using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace P02_Modbus
{
    /// <summary>
    /// P02：Modbus RTU 上位机（第一课）
    ///
    /// 本课目标：
    ///   1. 亲手实现 Modbus RTU 的帧结构、CRC16、03/06 功能码
    ///   2. 用"模拟从站"把整条链路跑通（不需要任何硬件）
    ///   3. 学会工程量的做法：寄存器是整数，要除以倍率
    ///   4. 学会上位机必须有的东西：超时、重试、报警、统计、存盘
    /// </summary>
    internal class Program
    {
        // ---- 配置区（现场改这里）----
        private const byte   SLAVE_ADDRESS = 1;
        private const int    SAMPLE_COUNT  = 30;      // 采多少个样本
        private const int    INTERVAL_MS   = 300;     // 采样周期
        private const double TEMP_HIGH     = 80.0;    // 温度上限 ℃
        private const double PRESS_HIGH    = 60.0;    // 压力上限 bar
        private const int    RETRY_TIMES   = 2;       // 通讯失败重试次数

        private static readonly List<Sample> _samples = new();

        private class Sample
        {
            public DateTime Time;
            public double Temp, Press, Speed;
            public ushort Status, Alarm, Product;
            public bool Ok;
            public string Error;
            public string[] Alarms = Array.Empty<string>();
        }

        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   P02  串口 / Modbus RTU 上位机                              ║");
            Console.WriteLine("║   第一课：协议核心 + 模拟从站 + 数据采集                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // ==================== 第 0 步：协议自检 ====================
            // 上位机开发最怕"协议写错了但看起来在跑"，所以先做自检
            Console.WriteLine("【第 0 步】协议自检");
            SelfTest();
            Console.WriteLine();

            // ==================== 第 1 步：连接设备 ====================
            using var device = new SimulatedModbusDevice(SLAVE_ADDRESS, latencyMs: 5);
            Console.WriteLine("【第 1 步】连接设备");

            if (!device.Connect())
            {
                Console.WriteLine("  ✗ 连接失败");
                return 1;
            }
            Console.WriteLine($"  ✓ 已连接：{device.Name}");
            Console.WriteLine();

            // ==================== 第 2 步：读一次，把原始帧打出来 ====================
            // 这一步非常重要：**永远先抓一帧原始字节**，你看得懂字节，才看得懂问题
            Console.WriteLine("【第 2 步】抓一帧原始报文（看懂字节比看懂界面重要）");
            DemoRawFrame(device);
            Console.WriteLine();

            // ==================== 第 3 步：循环采集 ====================
            Console.WriteLine("【第 3 步】开始采集");
            Console.WriteLine();
            Console.WriteLine("  时间         温度℃   压力bar   转速rpm   状态      报警     产量");
            Console.WriteLine("  ─────────────────────────────────────────────────────────────────────");

            for (int i = 1; i <= SAMPLE_COUNT; i++)
            {
                device.Slave.NextSample();          // 让模拟设备产生新的现场数据
                InjectFault(device, i);             // 按剧本制造通讯故障（教学用）

                var s = ReadOneSample(device, i);
                _samples.Add(s);
                PrintSample(s);

                System.Threading.Thread.Sleep(INTERVAL_MS);
            }

            // ==================== 第 4 步：统计 ====================
            Console.WriteLine();
            Console.WriteLine("【第 4 步】通讯统计");
            device.Stats.Print();
            Console.WriteLine();

            // ==================== 第 5 步：存盘 ====================
            Console.WriteLine("【第 5 步】存盘");
            SaveCsv();
            Console.WriteLine();

            // ==================== 第 6 步：回答"怎么换真设备" ====================
            Console.WriteLine("【第 6 步】换成真设备要改什么？");
            Console.WriteLine("  只换 IModbusDevice 的实现：");
            Console.WriteLine("    var device = new SimulatedModbusDevice(1);   // 今天：模拟");
            Console.WriteLine("    var device = new SerialModbusDevice(\"COM3\", 9600, 8, StopBits.One, Parity.None, 1);  // 明天：真串口");
            Console.WriteLine("  Program.cs 里其余代码 —— 一行都不用改。");
            Console.WriteLine();

            return 0;
        }

        // ====================================================================
        //  协议自检
        // ====================================================================
        private static void SelfTest()
        {
            // 自检 1：Modbus CRC 有一个固有性质 ——
            // 把 CRC 接在报文后面，再对整个（含 CRC）算一遍，结果必须是 0x0000
            var frame = ModbusRtu.BuildReadRequest(1, 0, 3);
            ushort whole = ModbusRtu.Crc16(frame, 0, frame.Length);
            if (whole != 0x0000)
            {
                Console.WriteLine("  ✗ CRC 自检失败！协议实现有问题，后面不用看了");
                Environment.Exit(2);
            }
            Console.WriteLine($"  ✓ CRC 环形自检通过（整帧再算一次 = 0x0000）");
            Console.WriteLine($"  ✓ 示例请求帧：{ModbusRtu.Hex(frame)}   ← 站号1 读地址0起3个寄存器");

            // 自检 2：故意改一个 bit，CRC 必须报错
            var bad = (byte[])frame.Clone();
            bad[3] ^= 0x01;
            bool detected = !ModbusRtu.CheckCrc(bad, bad.Length);
            Console.WriteLine($"  {(detected ? "✓" : "✗")} 单 bit 干扰检测：{(detected ? "已检出（校验生效）" : "未检出！")}");

            // 自检 3：读写往返
            var slave = new ModbusSlave(1);
            slave.NextSample();
            var req = ModbusRtu.BuildReadRequest(1, 0, 2);
            var resp = slave.Handle(req, req.Length);
            var vals = ModbusRtu.ParseReadResponse(resp, resp.Length, 1, 2);
            Console.WriteLine($"  ✓ 读写往返：响应帧 {ModbusRtu.Hex(resp)}  →  寄存器 [{vals[0]}, {vals[1]}]");
        }

        // ====================================================================
        //  打印原始报文
        // ====================================================================
        private static void DemoRawFrame(IModbusDevice device)
        {
            var req = ModbusRtu.BuildReadRequest(SLAVE_ADDRESS, 0, 5);
            Console.WriteLine($"  主站发出：{ModbusRtu.Hex(req)}");
            Console.WriteLine($"            └ 站号{req[0]} 功能码{req[1]:X2} 起始{req[2] * 256 + req[3]} 数量{req[4] * 256 + req[5]} CRC {req[6]:X2}{req[7]:X2}");

            var sb = (SimulatedModbusDevice)device;
            var resp = sb.Slave.Handle(req, req.Length);
            Console.WriteLine($"  从站回应：{ModbusRtu.Hex(resp)}");
            Console.WriteLine($"            └ 站号{resp[0]} 功能码{resp[1]:X2} 字节数{resp[2]}  数据...  CRC {resp[^2]:X2}{resp[^1]:X2}");
            Console.WriteLine();
            Console.WriteLine("  记住这个格式，抓包时一眼就能认出 Modbus：");
            Console.WriteLine("    [站号1B][功能码1B][数据nB][CRC 2B，低字节在前]");
        }

        // ====================================================================
        //  读一个样本（带重试 —— 现场必须这么做）
        // ====================================================================
        private static Sample ReadOneSample(IModbusDevice device, int index)
        {
            var s = new Sample { Time = DateTime.Now };

            for (int attempt = 0; attempt <= RETRY_TIMES; attempt++)
            {
                try
                {
                    var regs = device.ReadHoldingRegisters(SLAVE_ADDRESS, 0, 7);

                    // ---- 关键：寄存器是整数，要按地址表换算成工程量 ----
                    s.Temp    = regs[ModbusSlave.ADDR_TEMPERATURE] / 10.0;    // ×10 → ℃
                    s.Press   = regs[ModbusSlave.ADDR_PRESSURE] / 100.0;      // ×100 → bar
                    s.Speed   = regs[ModbusSlave.ADDR_SPEED];                 // 原值 rpm
                    s.Status  = regs[ModbusSlave.ADDR_STATUS];
                    s.Product = regs[ModbusSlave.ADDR_PRODUCT];
                    s.Alarm   = regs[ModbusSlave.ADDR_ALARM];                 // 设备自己的报警字
                    s.Ok = true;

                    // ---- 超限判断（上位机自己的报警，和设备报警字互相印证）----
                    var alarms = new List<string>();
                    if (s.Temp > TEMP_HIGH)      alarms.Add("温度高");
                    if (s.Press > PRESS_HIGH)    alarms.Add("压力高");
                    if ((s.Status & 0x0004) != 0) alarms.Add("设备故障");
                    s.Alarms = alarms.ToArray();

                    // ---- 顺手写一次设定值，演示功能码 06 ----
                    if (index == 5)
                    {
                        device.WriteSingleRegister(SLAVE_ADDRESS, ModbusSlave.ADDR_SETPOINT, 500);
                        Console.WriteLine("  ★ 演示 06 写单个寄存器：设定值 = 500  ← 写入成功");
                    }
                    return s;
                }
                catch (ModbusException ex)
                {
                    s.Ok = false;
                    s.Error = ex.Message;
                    if (attempt < RETRY_TIMES)
                    {
                        System.Threading.Thread.Sleep(60);
                        continue;   // 重试
                    }
                }
            }
            return s;
        }

        // ====================================================================
        //  打印一行
        // ====================================================================
        private static void PrintSample(Sample s)
        {
            if (!s.Ok)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  {s.Time:HH:mm:ss.fff}   ✗ 通讯失败：{s.Error}");
                Console.ResetColor();
                return;
            }

            string status = ((s.Status & 0x0001) != 0 ? "运行" : "停止")
                          + ((s.Status & 0x0002) != 0 ? "/报警" : "")
                          + ((s.Status & 0x0004) != 0 ? "/故障" : "");
            string alarm = s.Alarms.Length > 0 ? string.Join("+", s.Alarms) : "-";

            if (s.Alarms.Length > 0) Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  {s.Time:HH:mm:ss.fff}  {s.Temp,6:F1}  {s.Press,7:F2}  {s.Speed,8:F0}   {status,-10} {alarm,-10} {s.Product,5}");
            Console.ResetColor();
        }

        // ====================================================================
        //  按剧本注入通讯故障（教学：让你看到上位机怎么扛住）
        // ====================================================================
        private static void InjectFault(SimulatedModbusDevice device, int index)
        {
            var sl = device.Slave;
            sl.SimulateBrokenCrc    = index == 12 || index == 13;   // 线路干扰
            sl.SimulateNoResponse   = index == 18 || index == 19;   // 设备掉线
            sl.SimulateExceptionCode = (byte)(index == 24 ? 0x04 : 0); // 从站故障
        }

        // ====================================================================
        //  存 CSV
        // ====================================================================
        private static void SaveCsv()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "采集数据.csv");
            var sb = new StringBuilder();
            sb.AppendLine("时间,温度(℃),压力(bar),转速(rpm),状态字,报警字,产量,通讯正常,报警内容,错误信息");

            foreach (var s in _samples)
            {
                if (s.Ok)
                {
                    sb.AppendLine(string.Join(",",
                        s.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        s.Temp.ToString("F1", CultureInfo.InvariantCulture),
                        s.Press.ToString("F2", CultureInfo.InvariantCulture),
                        s.Speed.ToString("F0", CultureInfo.InvariantCulture),
                        $"0x{s.Status:X4}",
                        $"0x{s.Alarm:X4}",
                        s.Product,
                        "是",
                        s.Alarms.Length > 0 ? string.Join("+", s.Alarms) : "",
                        ""));
                }
                else
                {
                    sb.AppendLine(string.Join(",",
                        s.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        "", "", "", "", "", "", "否", "", s.Error ?? ""));
                }
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); // 带 BOM，Excel 直接打开不乱码

            int ok = 0, fail = 0, alarmRows = 0;
            foreach (var s in _samples) { if (s.Ok) { ok++; if (s.Alarms.Length > 0) alarmRows++; } else fail++; }

            var fi = new FileInfo(path);
            Console.WriteLine($"  ✓ {fi.Name}  ({fi.Length:N0} 字节)");
            Console.WriteLine($"    正常样本 {ok} 条，报警样本 {alarmRows} 条，通讯失败 {fail} 条");
            Console.WriteLine($"    路径：{path}");
        }
    }
}
