// ============================================================================
//  第 4 课   类与对象：把"一台设备"写成一个类
//
//  这一课是上位机开发的关键一步。真实上位机程序的骨架就是：
//
//      把每台设备抽象成一个对象  →  放进一个设备清单  →  定时轮询  →  判断报警
//
//  本课把这个骨架跑起来（数据先用模拟值；下一课再接真实通讯）。
//
//  运行方式：
//      dotnet run --project L04-Classes
// ============================================================================


// ---------------------------------------------------------------------------
// 1. 创建对象（实例化）
//
//    类是"图纸"，对象是按这张图纸造出来的"实物"。
//    同一个类可以造出很多个对象，它们的数据各自独立。
// ---------------------------------------------------------------------------

Device d1 = new Device();
d1.Name = "注液机-01";
d1.Model = "ZY-200";
d1.AlarmLimit = 85;

Device d2 = new Device();
d2.Name = "烘箱-02";
d2.Model = "OV-500";
d2.AlarmLimit = 120;

Console.WriteLine("=== 1. 同一个类，造出两个互相独立的对象 ===");
Console.WriteLine($"  d1 = {d1.Name} / {d1.Model} / 上限 {d1.AlarmLimit} ℃");
Console.WriteLine($"  d2 = {d2.Name} / {d2.Model} / 上限 {d2.AlarmLimit} ℃");


// ---------------------------------------------------------------------------
// 2. 调用方法：对象自己的行为
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 2. 连接设备 ===");
d1.Connect();
d2.Connect();


// ---------------------------------------------------------------------------
// 3. 封装的意义：方法里可以加校验
//
//    注意下面 d3 在"未连接"状态下写数据会被拒绝 —— 这就是 private 字段的价值。
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 3. 封装：没连接就不让写数据 ===");

Device d3 = new Device();
d3.Name = "未接线的表-03";
d3.Model = "XX-100";
d3.AlarmLimit = 60;

d3.SetTemperature(50);           // 还没连接 → 会被拒绝
d3.Connect();
d3.SetTemperature(50);           // 这次成功
Console.WriteLine($"  d3 当前温度 = {d3.GetTemperature()} ℃");


// ---------------------------------------------------------------------------
// 4. 把多台设备放进列表 —— 这就是上位机的"设备清单"
//
//    真实项目里，设备清单往往是从配置文件读出来的（L06 会做）。
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 4. 设备清单 List<Device> ===");

List<Device> devices = new List<Device>();
devices.Add(d1);
devices.Add(d2);
devices.Add(d3);

Console.WriteLine($"  清单里共 {devices.Count} 台设备：");
foreach (Device d in devices)
{
    Console.WriteLine($"    - {d.Name}");
}


// ---------------------------------------------------------------------------
// 5. 模拟上位机主循环：定时轮询 + 报警判断
//
//    【这就是这一课的重点】真实上位机的 while 循环里干的事，就是这个：
//        读一批数据 → 逐个设备更新 → 判断报警 → 记录 → 等下一个周期
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 5. 模拟轮询 5 轮（上位机主循环的骨架）===");

// 模拟每台设备每轮读到的温度（为了演示，先写死）
double[,] simulated =
{
    {  62.5,  70.1,  88.6,  85.0,  90.2 },     // d1
    {  95.0, 118.0, 121.5, 119.0, 122.8 },     // d2
    {  50.0,  55.5,  60.0,  65.3,  59.1 },     // d3
};

int alarmCount = 0;

for (int round = 1; round <= 5; round++)
{
    Console.WriteLine($"  --- 第 {round} 轮 ---");

    for (int i = 0; i < devices.Count; i++)
    {
        Device d = devices[i];

        // 真实项目里，这一行会换成：从串口 / Modbus 读回一个真实数值
        d.SetTemperature(simulated[i, round - 1]);

        Console.WriteLine("    " + d.Describe());

        if (d.IsAlarming()) { alarmCount++; }
    }
}

Console.WriteLine();
Console.WriteLine($"  累计报警 {alarmCount} 次");


// ---------------------------------------------------------------------------
// 6. 统计：每台设备被读取了多少次
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 6. 每台设备的读取次数 ===");
foreach (Device d in devices)
{
    Console.WriteLine($"  {d.Name,-12} 读了 {d.GetReadCount()} 次");
}


// ---------------------------------------------------------------------------
// 7. 收尾：断开全部设备
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 7. 断开全部设备 ===");
foreach (Device d in devices)
{
    d.Disconnect();
}


Console.WriteLine();
Console.WriteLine("第 4 课结束。");
Console.WriteLine("练习：给 Device 加一个下限 LowerLimit，再写一个 IsOutOfRange() 方法，");
Console.WriteLine("      把主循环改成同时判断上限和下限。");
