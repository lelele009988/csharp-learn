using System;
using System.Collections.Generic;

namespace P02_Modbus
{
    /// <summary>
    /// Modbus RTU 协议核心。
    ///
    /// 一个 RTU 帧的通用结构：
    ///   [站号 1B] [功能码 1B] [数据 nB] [CRC16 2B，低字节在前]
    ///
    /// 记住三件事：
    ///   1. 帧与帧之间靠【时间间隔】分隔（3.5 个字符时间），不是靠长度字段
    ///   2. CRC 是【低字节在前】，和寄存器数据的高字节在前正好相反
    ///   3. 从站出错时功能码最高位置 1（03 -> 83），后面跟 1 字节异常码
    /// </summary>
    public static class ModbusRtu
    {
        /// <summary>常用功能码</summary>
        public const byte FC_READ_HOLDING_REGISTERS = 0x03;  // 读保持寄存器
        public const byte FC_READ_INPUT_REGISTERS   = 0x04;  // 读输入寄存器
        public const byte FC_WRITE_SINGLE_REGISTER  = 0x06;  // 写单个寄存器
        public const byte FC_WRITE_MULTIPLE         = 0x10;  // 写多个寄存器

        /// <summary>
        /// 计算 CRC16（Modbus 专用）。
        /// 算法：初值 0xFFFF；每字节异或后右移 8 次；移出位为 1 就异或 0xA001。
        /// 注意这个多项式是【反向】表示的（0xA001 = 0x8005 的镜像）。
        /// </summary>
        public static ushort Crc16(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    bool lsb = (crc & 0x0001) != 0;
                    crc >>= 1;
                    if (lsb) crc ^= 0xA001;
                }
            }
            return crc;
        }

        /// <summary>给一帧补上末尾的 CRC（低字节在前）</summary>
        public static byte[] WithCrc(byte[] body)
        {
            ushort crc = Crc16(body, 0, body.Length);
            var frame = new byte[body.Length + 2];
            Array.Copy(body, frame, body.Length);
            frame[body.Length]     = (byte)(crc & 0xFF);   // 低字节先发
            frame[body.Length + 1] = (byte)(crc >> 8);
            return frame;
        }

        /// <summary>校验一帧的 CRC 是否正确</summary>
        public static bool CheckCrc(byte[] frame, int length)
        {
            if (length < 4) return false;
            ushort calc = Crc16(frame, 0, length - 2);
            ushort recv = (ushort)(frame[length - 2] | (frame[length - 1] << 8));
            return calc == recv;
        }

        // ==================== 组帧（主站 -> 从站） ====================

        /// <summary>
        /// 组"读保持寄存器"请求帧。例：站号 1、起始 0、数量 3
        ///   01 03 00 00 00 03 xx xx
        /// </summary>
        public static byte[] BuildReadRequest(byte slave, ushort start, ushort count)
        {
            var body = new byte[6];
            body[0] = slave;
            body[1] = FC_READ_HOLDING_REGISTERS;
            body[2] = (byte)(start >> 8);   // 起始地址：高字节在前
            body[3] = (byte)(start & 0xFF);
            body[4] = (byte)(count >> 8);   // 寄存器数量：高字节在前
            body[5] = (byte)(count & 0xFF);
            return WithCrc(body);
        }

        /// <summary>组"写单个寄存器"请求帧。例：站号 1、地址 10、值 500
        ///   01 06 00 0A 01 F4 xx xx
        /// </summary>
        public static byte[] BuildWriteSingleRequest(byte slave, ushort addr, ushort value)
        {
            var body = new byte[6];
            body[0] = slave;
            body[1] = FC_WRITE_SINGLE_REGISTER;
            body[2] = (byte)(addr >> 8);
            body[3] = (byte)(addr & 0xFF);
            body[4] = (byte)(value >> 8);
            body[5] = (byte)(value & 0xFF);
            return WithCrc(body);
        }

        // ==================== 解析（从站 -> 主站） ====================

        /// <summary>
        /// 解析"读保持寄存器"响应帧。成功返回寄存器数组；失败抛异常（带原因）。
        /// 正常响应： 01 03 06 [v0H v0L] [v1H v1L] [v2H v2L] CRC
        /// 异常响应： 01 83 [异常码] CRC
        /// </summary>
        public static ushort[] ParseReadResponse(byte[] resp, int length, byte slave, ushort expectedCount)
        {
            if (resp == null || length < 5)
                throw new ModbusException($"响应太短（{length} 字节）");

            if (resp[0] != slave)
                throw new ModbusException($"站号不符：期望 {slave}，收到 {resp[0]}");

            // 异常响应：功能码 = 原功能码 | 0x80
            if ((resp[1] & 0x80) != 0)
            {
                byte code = resp[1] == 0x83 ? (length > 2 ? resp[2] : (byte)0) : (byte)0;
                throw new ModbusException($"从站异常，功能码 0x{resp[1]:X2}，异常码 0x{code:X2}（{ExplainException(code)}）");
            }

            if (resp[1] != FC_READ_HOLDING_REGISTERS)
                throw new ModbusException($"功能码不符：期望 03，收到 0x{resp[1]:X2}");

            int byteCount = resp[2];
            int need = 3 + byteCount + 2;
            if (length < need)
                throw new ModbusException($"响应长度不足：需要 {need}，实际 {length}");
            if (byteCount != expectedCount * 2)
                throw new ModbusException($"字节数不符：期望 {expectedCount * 2}，收到 {byteCount}");
            if (!CheckCrc(resp, need))
                throw new ModbusException("CRC 校验失败（数据在传输中被破坏）");

            var values = new ushort[expectedCount];
            for (int i = 0; i < expectedCount; i++)
            {
                // 寄存器值：高字节在前
                values[i] = (ushort)((resp[3 + i * 2] << 8) | resp[4 + i * 2]);
            }
            return values;
        }

        /// <summary>异常码含义（现场排障时很有用）</summary>
        public static string ExplainException(byte code)
        {
            return code switch
            {
                0x01 => "非法功能码",
                0x02 => "非法数据地址",
                0x03 => "非法数据值",
                0x04 => "从站设备故障",
                0x05 => "确认（已接收，正在长时间处理）",
                0x06 => "从站设备忙",
                0x08 => "存储奇偶性差错",
                0x0A => "网关路径不可用",
                0x0B => "网关目标设备无响应",
                _    => "未知异常码"
            };
        }

        /// <summary>把字节数组格式化成 "01 03 00 00" 这种形式，抓包对比用</summary>
        public static string Hex(byte[] data, int length = -1)
        {
            if (data == null) return "(null)";
            int n = length < 0 ? data.Length : length;
            var sb = new System.Text.StringBuilder(n * 3);
            for (int i = 0; i < n && i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    /// <summary>Modbus 通讯异常</summary>
    public class ModbusException : Exception
    {
        public ModbusException(string message) : base(message) { }
    }
}
