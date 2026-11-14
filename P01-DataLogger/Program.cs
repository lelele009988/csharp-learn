// ============================================================================
//  P01 · 数据采集小上位机（控制台版）
//
//  这就是真实上位机程序的骨架，五个部分：
//
//      ① 建立设备清单      ← L04 学的"类与对象"，在这里升级成实用版
//      ② 打开数据文件      ← 带表头，Excel 能直接双击打开
//      ③ 采集主循环        ← 定时轮询 + 判断报警
//      ④ 落盘              ← 数据 CSV / 报警 CSV / 运行日志，三份文件
//      ⑤ 结束统计          ← 每路的最大/最小/平均/报警次数
//
//  这一版数据是【模拟】的（用正弦波 + 噪声造出来，还会模拟一次掉线）。
//  下一步 P02 只要把 Simulate() 换成"读串口 / 读 Modbus"，就是真上位机。
//
//  运行： dotnet run --project P01-DataLogger
// ============================================================================

using System.Text;

// ---------------------------------------------------------------------------
// 【配置区】把可能要改的东西全放最上面
// ---------------------------------------------------------------------------
const string DataFile   = "采集数据.csv";
const string AlarmFile  = "报警记录.csv";
const string LogFile    = "运行日志.txt";
const int    Rounds     = 50;      // 采集多少轮（真实项目里是 while(true)）
const int    IntervalMs = 100;     // 每轮间隔（毫秒）

// UTF-8 带 BOM：这样中文 CSV 用 Excel 双击打开不会乱码
// 如果写成 new UTF8Encoding(false)（不带 BOM），Excel 会显示成乱码
var utf8Bom = new UTF8Encoding(true);


// ---------------------------------------------------------------------------
// ① 建立设备清单
//
// 这里用了"工厂方法"的小技巧：用一个函数统一造设备，避免四段重复代码。
// （重复代码是 bug 的温床：改一处忘一处）
// ---------------------------------------------------------------------------
static Device CreateDevice(string name, string unit, double lower, double upper)
{
    var d = new Device();
    d.Name       = name;
    d.Unit       = unit;
    d.LowerLimit = lower;
    d.UpperLimit = upper;
    d.Connect();                 // 建好就连接
    return d;
}

var devices = new List<Device>();
devices.Add(CreateDevice("1#温度", "℃",    20.0,  40.0));
devices.Add(CreateDevice("2#温度", "℃",    20.0,  40.0));
devices.Add(CreateDevice("1#压力", "kPa",  95.0, 105.0));
devices.Add(CreateDevice("2#压力", "kPa",  95.0, 105.0));

Console.WriteLine($"=== P01 数据采集小上位机 ===");
Console.WriteLine($"设备清单：{devices.Count} 路通道");
foreach (var d in devices)
{
    Console.WriteLine($"   {d.Name}   量程 {d.LowerLimit} ~ {d.UpperLimit} {d.Unit}");
}


// ---------------------------------------------------------------------------
// ② 准备三个文件
// ---------------------------------------------------------------------------

// using var：变量离开作用域时自动调用 Dispose() 关闭文件。
// 比手动 Save/Close 更安全——即使程序中途抛异常，文件也会被正确关闭。
using var dataWriter = new StreamWriter(DataFile, false, utf8Bom) { AutoFlush = true };

// 数据文件表头
{
    var head = new List<string> { "轮次", "时间" };
    foreach (var d in devices)
    {
        head.Add($"{d.Name}({d.Unit})");       // 例如 "1#温度(℃)"
    }
    dataWriter.WriteLine(string.Join(",", head));
}

// 报警文件：先写表头（覆盖），后面每次报警追加一行
File.WriteAllText(AlarmFile, $"时间,设备,数值,下限,上限,类型{Environment.NewLine}", utf8Bom);

// 运行日志：清空重建
File.WriteAllText(LogFile, "");


// ---------------------------------------------------------------------------
// ③ 采集主循环
// ---------------------------------------------------------------------------

// --- 模拟数据源：正弦波（慢速漂移）+ 随机噪声 ---
// 真实项目里，把这个函数换成"从串口/Modbus 读一个数"即可
var rnd = new Random(20261018);                        // 固定种子 → 每次运行结果一致，方便复现
double[] baseValues = { 30.0, 30.0, 100.0, 100.0 };    // 基准值
double[] amplitudes = { 11.0, 11.0,   6.5,   6.5 };    // 波动幅度（决定会不会超限）

double Simulate(int index, int round)
{
    double phase = index * 1.3;                                        // 每路相位错开
    double wave  = amplitudes[index] * Math.Sin(2.0 * Math.PI * round / 30.0 + phase);
    double noise = (rnd.NextDouble() - 0.5) * 0.8;                     // ±0.4 的噪声
    return baseValues[index] + wave + noise;
}

// --- 表头 ---
Console.WriteLine();
Console.WriteLine($"{"轮次",4}  时间           " +
                  string.Join("  ", devices.Select(d => $"{d.Name,8}")) + "   报警");
Console.WriteLine(new string('-', 86));

File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 采集开始，共 {Rounds} 轮{Environment.NewLine}");

int totalAlarms = 0;

// 【异常处理】整个采集循环包在 try/catch 里。
// 真实项目里，串口被拔、PLC 断电、文件被 Excel 占用，都会在这里被接住，
// 而不是让程序闪退——现场人员最怕的就是"软件没了"。
try
{
    for (int round = 1; round <= Rounds; round++)
    {
        // --- 模拟第 30~33 轮 2#温度 掉线，检验掉线处理 ---
        if (round == 30)
        {
            devices[1].Disconnect();
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] 2#温度 通讯中断（模拟）{Environment.NewLine}");
        }
        else if (round == 34)
        {
            devices[1].Connect();
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] 2#温度 通讯恢复{Environment.NewLine}");
        }

        DateTime time = DateTime.Now;
        var cells     = new List<string>();     // 显示用（带宽度格式）
        var csvCells  = new List<string>();     // 落盘用（原始值）
        var alarmLines = new List<string>();    // 本轮产生的报警行

        for (int i = 0; i < devices.Count; i++)
        {
            Device d = devices[i];

            if (!d.IsConnected)
            {
                // 掉线：显示 "--"，CSV 留空，不更新统计
                cells.Add($"{"--",8}");
                csvCells.Add("");
                continue;
            }

            double raw = Simulate(i, round);
            bool   alarm = d.Update(raw);

            cells.Add($"{raw,8:F2}");
            csvCells.Add(raw.ToString("F2"));

            if (alarm)
            {
                totalAlarms++;
                alarmLines.Add($"{time:HH:mm:ss.fff},{d.Name},{raw:F2},{d.LowerLimit},{d.UpperLimit},{d.AlarmText()}");
            }
        }

        // --- 显示本轮 ---
        string alarmText = "";
        if (alarmLines.Count > 0)
        {
            var brief = new List<string>();
            for (int i = 0; i < devices.Count; i++)
            {
                Device d = devices[i];
                if (d.IsConnected && d.IsAlarming() && csvCells[i] != "")
                {
                    brief.Add($"{d.Name} {d.AlarmText()}");
                }
            }
            alarmText = "   << " + string.Join(" / ", brief);
        }

        string line = $"{round,4}  {time:HH:mm:ss.fff}   " + string.Join("  ", cells) + alarmText;

        if (alarmLines.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;   // 报警标红
            Console.WriteLine(line);
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(line);
        }

        // --- 落盘 ---
        dataWriter.WriteLine($"{round},{time:HH:mm:ss.fff},{string.Join(",", csvCells)}");

        if (alarmLines.Count > 0)
        {
            File.AppendAllLines(AlarmFile, alarmLines, utf8Bom);
        }

        // --- 等下一个周期 ---
        // 真实项目里这里通常会换成定时器或 async/await，避免阻塞界面线程
        Thread.Sleep(IntervalMs);
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"采集异常终止：{ex.Message}");
    Console.ResetColor();
    File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] 异常：{ex}{Environment.NewLine}");
}


// ---------------------------------------------------------------------------
// ⑤ 结束统计
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 采集结束 ===");
Console.WriteLine($"总报警次数：{totalAlarms}");
Console.WriteLine();
Console.WriteLine($"{"设备",-10}{"采样数",8}{"最小",10}{"最大",10}{"平均",10}{"报警",8}");
Console.WriteLine(new string('-', 58));
foreach (var d in devices)
{
    Console.WriteLine($"{d.Name,-10}{d.ReadCount,8}{d.Min,10:F2}{d.Max,10:F2}{d.Average,10:F2}{d.AlarmCount,8}");
}

Console.WriteLine();
Console.WriteLine($"数据文件：{Path.GetFullPath(DataFile)}");
Console.WriteLine($"报警文件：{Path.GetFullPath(AlarmFile)}");
Console.WriteLine($"日志文件：{Path.GetFullPath(LogFile)}");

File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 采集结束，共 {totalAlarms} 次报警{Environment.NewLine}");
