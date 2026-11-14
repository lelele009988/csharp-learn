using System;

namespace P02_Modbus
{
    /// <summary>
    /// 设备抽象接口 —— 这是整个 P02 的"骨架"。
    ///
    /// 为什么先定接口？用你熟的话说：
    ///   这就是 PLC 里的【FB 类型定义】，具体设备就是【背景数据块 / 实例】。
    ///   上位机代码只对着接口写；今天用模拟设备，明天换真串口，后天换 TCP，
    ///   Program.cs 一行都不用改。
    ///
    /// 这叫"依赖倒置"，是工程里最值钱的一条习惯。
    /// </summary>
    public interface IModbusDevice : IDisposable
    {
        bool IsConnected { get; }
        string Name { get; }

        bool Connect();
        void Disconnect();

        /// <summary>读保持寄存器（功能码 03）。失败抛 ModbusException。</summary>
        ushort[] ReadHoldingRegisters(byte slave, ushort start, ushort count);

        /// <summary>写单个寄存器（功能码 06）。失败抛 ModbusException。</summary>
        void WriteSingleRegister(byte slave, ushort addr, ushort value);

        /// <summary>通讯统计（现场排查必看）</summary>
        CommStats Stats { get; }
    }

    /// <summary>通讯统计</summary>
    public class CommStats
    {
        public int Requests;      // 发出的请求
        public int Success;       // 成功
        public int Timeouts;      // 超时（从站没应答）
        public int CrcErrors;     // CRC 错（线路干扰）
        public int Exceptions;    // 从站返回异常码

        public double SuccessRate => Requests == 0 ? 0 : (double)Success / Requests * 100.0;

        public void Print()
        {
            Console.WriteLine($"  请求 {Requests}  成功 {Success}  超时 {Timeouts}  CRC错 {CrcErrors}  异常 {Exceptions}  成功率 {SuccessRate:F1}%");
        }
    }

    /// <summary>
    /// 模拟设备：走【真实的帧编解码】，只是"线路"换成了内存里的一次函数调用。
    ///
    /// 注意它是怎么工作的：
    ///   1. 上层调用 ReadHoldingRegisters(...)
    ///   2. 这里组出真实的请求帧字节
    ///   3. 交给 ModbusSlave.Handle() —— 从站按协议处理
    ///   4. 拿回真实响应帧字节，按协议解析成值
    ///
    /// 所以协议代码是**真跑到了**，不是假的。以后换成串口，只把第 3 步换成
    /// "写进 SerialPort 再读回来"即可。
    /// </summary>
    public class SimulatedModbusDevice : IModbusDevice
    {
        private readonly ModbusSlave _slave;
        private readonly int _latencyMs;

        public bool IsConnected { get; private set; }
        public string Name => $"模拟设备（站号 {_slave.SlaveAddress}）";
        public CommStats Stats { get; } = new CommStats();
        public ModbusSlave Slave => _slave;

        public SimulatedModbusDevice(byte slaveAddress, int latencyMs = 0)
        {
            _slave = new ModbusSlave(slaveAddress);
            _latencyMs = latencyMs;
        }

        public bool Connect()
        {
            IsConnected = true;
            return true;
        }

        public void Disconnect() => IsConnected = false;

        public ushort[] ReadHoldingRegisters(byte slave, ushort start, ushort count)
        {
            var resp = Transact(ModbusRtu.BuildReadRequest(slave, start, count), 3 + count * 2 + 2);
            // 解析失败会抛 ModbusException，由上层 catch
            return ModbusRtu.ParseReadResponse(resp, resp.Length, slave, count);
        }

        public void WriteSingleRegister(byte slave, ushort addr, ushort value)
        {
            var resp = Transact(ModbusRtu.BuildWriteSingleRequest(slave, addr, value), 8);
            if (resp.Length < 8) throw new ModbusException($"写响应太短（{resp.Length}）");
            if (resp[0] != slave) throw new ModbusException("写响应站号不符");
            if (resp[1] != ModbusRtu.FC_WRITE_SINGLE_REGISTER)
                throw new ModbusException($"写响应功能码异常 0x{resp[1]:X2}");
            if (!ModbusRtu.CheckCrc(resp, resp.Length)) throw new ModbusException("写响应 CRC 校验失败");
        }

        /// <summary>一次完整事务：发请求 -> 收响应。真实串口里这里就是 Write + Read</summary>
        private byte[] Transact(byte[] request, int expectedLen)
        {
            if (!IsConnected) throw new ModbusException("设备未连接");
            Stats.Requests++;

            if (_latencyMs > 0) System.Threading.Thread.Sleep(_latencyMs);

            byte[] resp = _slave.Handle(request, request.Length);

            // 从站不应答 = 超时（真实世界里主站等 200~1000ms 才判定超时）
            if (resp == null)
            {
                Stats.Timeouts++;
                throw new ModbusException("应答超时（从站无响应）");
            }

            // 检查是不是异常响应（功能码最高位）
            if ((resp[1] & 0x80) != 0 && resp.Length >= 5)
            {
                Stats.Exceptions++;
                byte code = resp[2];
                throw new ModbusException($"从站异常 0x{code:X2}（{ModbusRtu.ExplainException(code)}）");
            }

            // CRC 错
            if (!ModbusRtu.CheckCrc(resp, resp.Length))
            {
                Stats.CrcErrors++;
                throw new ModbusException("CRC 校验失败");
            }

            Stats.Success++;
            return resp;
        }

        public void Dispose() => Disconnect();
    }
}
