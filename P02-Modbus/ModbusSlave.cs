using System;

namespace P02_Modbus
{
    /// <summary>
    /// 模拟的 Modbus 从站（相当于一个 PLC / 仪表）。
    ///
    /// 它实现的是【从站那一侧】：收到请求帧 -> 校验 -> 按功能码回应 -> 返回响应帧。
    ///
    /// 为什么要自己写一个从站？
    ///   1. 你机器上没有 Modbus 设备，不写它就跑不起来
    ///   2. 写完你会发现"主站/从站是一对镜像" —— 看懂一边，另一边自然懂
    ///   3. 以后接真 PLC，把这一层换成真串口即可，上面的代码一行不改
    /// </summary>
    public class ModbusSlave
    {
        public const ushort REG_COUNT = 8;

        public byte SlaveAddress { get; }
        public ushort[] Registers { get; } = new ushort[REG_COUNT];

        // ---- 故障注入开关（用来测主站的容错）----
        /// <summary>模拟"从站没应答"（掉线 / 波特率不对）</summary>
        public bool SimulateNoResponse { get; set; }
        /// <summary>模拟"从站返回异常响应"（如地址越界）</summary>
        public byte SimulateExceptionCode { get; set; }
        /// <summary>模拟"CRC 被破坏"（线路干扰）</summary>
        public bool SimulateBrokenCrc { get; set; }

        /// <summary>从站收到的请求次数（诊断用）</summary>
        public int RequestCount { get; private set; }

        private int _step;

        public ModbusSlave(byte slaveAddress)
        {
            SlaveAddress = slaveAddress;
        }

        // ==================== 寄存器地址表 ====================
        // 现场设备都会有一张"地址表"，这是上位机开发的起点
        public const ushort ADDR_TEMPERATURE = 0;   // 温度  ×10   例：685 -> 68.5 ℃
        public const ushort ADDR_PRESSURE    = 1;   // 压力  ×100  例：4523 -> 45.23 bar
        public const ushort ADDR_SPEED       = 2;   // 转速  rpm   例：1580 -> 1580 rpm
        public const ushort ADDR_STATUS      = 3;   // 状态字（按位）
        public const ushort ADDR_PRODUCT     = 4;   // 产量计数
        public const ushort ADDR_SETPOINT    = 5;   // 设定值（**可写**）
        public const ushort ADDR_ALARM       = 6;   // 报警字（按位）
        public const ushort ADDR_RESERVED    = 7;

        /// <summary>产生下一拍"现场数据"（模拟工艺过程在变化）</summary>
        public void NextSample()
        {
            _step++;

            double t = _step;
            // 温度：35 ~ 85 ℃ 之间缓慢摆动
            double temp = 60.0 + 25.0 * Math.Sin(t / 6.0);
            // 压力：25 ~ 65 bar（相位错开，开机时先是正常值）
            double press = 45.0 + 20.0 * Math.Sin(t / 9.0);
            // 转速：1200 ~ 1800 rpm
            double rpm = 1500.0 + 300.0 * Math.Sin(t / 5.0);

            Registers[ADDR_TEMPERATURE] = (ushort)Math.Round(temp * 10);    // ×10
            Registers[ADDR_PRESSURE]    = (ushort)Math.Round(press * 100);  // ×100
            Registers[ADDR_SPEED]       = (ushort)Math.Round(rpm);
            Registers[ADDR_PRODUCT]     = (ushort)(_step * 3);

            // 状态字：bit0=运行  bit1=报警  bit2=故障
            ushort status = 0x0001;                                  // 运行中
            ushort alarm  = 0;
            if (temp > 80.0)  { status |= 0x0002; alarm |= 0x0001; } // 温度高
            if (press > 60.0) { status |= 0x0002; alarm |= 0x0002; } // 压力高
            if (_step > 26 && _step < 29) { status |= 0x0004; }      // 模拟一段"故障"
            Registers[ADDR_STATUS] = status;
            Registers[ADDR_ALARM]  = alarm;
        }

        /// <summary>
        /// 处理一帧请求，返回响应帧；返回 null 表示【不应答】（真实 Modbus 就是这样：
        /// 站号不对、CRC 错、设备掉线，从站都是静默的，主站只能靠超时判断）。
        /// </summary>
        public byte[] Handle(byte[] request, int length)
        {
            RequestCount++;

            if (SimulateNoResponse) return null;
            if (length < 4) return null;
            if (!ModbusRtu.CheckCrc(request, length)) return null;     // CRC 错：静默丢弃
            if (request[0] != SlaveAddress) return null;               // 不是找我的

            byte fc = request[1];

            // ---- 03 读保持寄存器 ----
            if (fc == ModbusRtu.FC_READ_HOLDING_REGISTERS)
            {
                if (SimulateExceptionCode != 0)
                    return BuildExceptionResponse(SimulateExceptionCode);

                ushort start = (ushort)((request[2] << 8) | request[3]);
                ushort count = (ushort)((request[4] << 8) | request[5]);

                if (count < 1 || count > 125) return BuildExceptionResponse(0x03); // 非法数据值
                if (start + count > REG_COUNT) return BuildExceptionResponse(0x02); // 非法数据地址

                var body = new byte[3 + count * 2];
                body[0] = SlaveAddress;
                body[1] = fc;
                body[2] = (byte)(count * 2);
                for (int i = 0; i < count; i++)
                {
                    ushort v = Registers[start + i];
                    body[3 + i * 2]     = (byte)(v >> 8);    // 高字节在前
                    body[4 + i * 2]     = (byte)(v & 0xFF);
                }
                return Corrupt(ModbusRtu.WithCrc(body));
            }

            // ---- 06 写单个寄存器 ----
            if (fc == ModbusRtu.FC_WRITE_SINGLE_REGISTER)
            {
                if (SimulateExceptionCode != 0)
                    return BuildExceptionResponse(SimulateExceptionCode);

                ushort addr  = (ushort)((request[2] << 8) | request[3]);
                ushort value = (ushort)((request[4] << 8) | request[5]);

                if (addr >= REG_COUNT) return BuildExceptionResponse(0x02);
                if (addr != ADDR_SETPOINT) return BuildExceptionResponse(0x02); // 只允许写设定值

                Registers[addr] = value;

                // 06 的正常响应 = 把请求原样回显
                var body = new byte[6];
                Array.Copy(request, body, 6);
                return Corrupt(ModbusRtu.WithCrc(body));
            }

            // ---- 不支持的功能码 ----
            return BuildExceptionResponse(0x01);
        }

        private byte[] BuildExceptionResponse(byte code)
        {
            var body = new byte[3];
            body[0] = SlaveAddress;
            body[1] = 0x83;   // 03 | 0x80
            body[2] = code;
            return Corrupt(ModbusRtu.WithCrc(body));
        }

        /// <summary>故障注入：故意破坏 CRC 的最后一个字节</summary>
        private byte[] Corrupt(byte[] frame)
        {
            if (SimulateBrokenCrc && frame.Length > 2)
                frame[frame.Length - 1] ^= 0xFF;
            return frame;
        }
    }
}
