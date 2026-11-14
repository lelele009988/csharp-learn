// ============================================================================
//  Device.cs —— 一台"设备"的模型
//
//  【重要约定】在真实的 C# 项目里，规矩是"一个类一个文件"，文件名 = 类名。
//  所以你在这里看到的 Device，在正经项目里就应该是 Device.cs 这个文件。
//
//  这个类把"一台设备"所需要的数据和行为打包在一起：
//    - 数据（字段）：名字、型号、连接状态、报警上限、当前温度……
//    - 行为（方法）：连接、断开、写入读数、判断报警、输出一行状态
// ============================================================================


/// <summary>
/// 模拟一台工业设备（一路温度采集通道）。
/// </summary>
class Device
{
    // ------------------------------------------------------------------------
    // 【public 字段】外部可以直接读写
    // ------------------------------------------------------------------------

    /// <summary>设备名称，例如 "注液机-01"</summary>
    public string Name;

    /// <summary>设备型号</summary>
    public string Model;

    /// <summary>是否已连接（相当于 PLC 里的一个状态位）</summary>
    public bool IsConnected;

    /// <summary>报警上限（℃，超过就报警）</summary>
    public double AlarmLimit;


    // ------------------------------------------------------------------------
    // 【private 字段】外部不能直接碰，必须通过方法来改
    //
    // 这叫"封装"。好处是：可以在方法内部加校验、计数、触发报警……
    // 如果 _temperature 是 public，外部就能随便乱赋值，你就失去了控制权。
    // ------------------------------------------------------------------------

    private double _temperature;      // 当前温度
    private int _readCount;           // 本设备累计被读取了多少次


    // ------------------------------------------------------------------------
    // 方法：这台设备的"行为"
    // ------------------------------------------------------------------------

    /// <summary>连接设备</summary>
    public void Connect()
    {
        IsConnected = true;
        Console.WriteLine($"  [{Name}] 已连接");
    }

    /// <summary>断开设备</summary>
    public void Disconnect()
    {
        IsConnected = false;
        Console.WriteLine($"  [{Name}] 已断开");
    }

    /// <summary>
    /// 写入一次读数。
    /// 真实项目里，这个方法的内部就是"读串口"或"读 Modbus 寄存器"的代码。
    /// </summary>
    public void SetTemperature(double value)
    {
        // 封装的好处就在这里：可以拦截不合理的情况
        if (!IsConnected)
        {
            Console.WriteLine($"  [{Name}] 未连接，拒绝写入数据");
            return;
        }

        _temperature = value;
        _readCount++;
    }

    /// <summary>读取当前温度</summary>
    public double GetTemperature()
    {
        return _temperature;
    }

    /// <summary>是否超限报警</summary>
    public bool IsAlarming()
    {
        return IsConnected && _temperature >= AlarmLimit;
    }

    /// <summary>返回一行状态描述，方便直接在控制台打印</summary>
    public string Describe()
    {
        string state = IsConnected ? "已连接" : "未连接";
        string alarm = IsAlarming() ? "   <<< 报警!" : "";
        return $"{Name,-12} [{Model,-8}] {state}  温度 {_temperature,6:F1} ℃ / 上限 {AlarmLimit,6:F1} ℃{alarm}";
    }

    /// <summary>本设备累计被读取次数</summary>
    public int GetReadCount()
    {
        return _readCount;
    }
}
