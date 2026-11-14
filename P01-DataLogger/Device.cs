// ============================================================================
//  Device.cs —— 一路被采集的通道（"一台设备"）
//
//  相比 L04 的版本，这里多了三样上位机必需的东西：
//    1) 上下限 + 报警判断（L04 只有上限）
//    2) 内部统计：采样次数、最小值、最大值、平均值、报警次数
//    3) 连接状态：掉线时拒绝写入数据（模拟通讯中断）
//
//  【关键设计原则】
//  所有"统计"都在 Device 内部算，外部不再自己遍历数组去算。
//  这样主程序只管"采集 + 显示"，逻辑清晰。
// ============================================================================

class Device
{
    // ------------------------------------------------------------------------
    // 配置项：外部可以直接读写（相当于 PLC 功能块的输入引脚）
    // ------------------------------------------------------------------------

    public string Name;         // 通道名，例如 "1#温度"
    public string Unit;         // 单位，例如 "℃"、"kPa"
    public double LowerLimit;   // 报警下限
    public double UpperLimit;   // 报警上限
    public bool IsConnected;    // 通讯是否正常


    // ------------------------------------------------------------------------
    // 内部状态：private，外部只能读不能改（相当于功能块的内部变量）
    // ------------------------------------------------------------------------

    private double _value;                          // 最新一次采样值
    private int _readCount;                         // 成功采样次数
    private int _alarmCount;                        // 报警次数
    private double _sum;                            // 累加和（用来算平均）
    private double _min = double.MaxValue;          // 最小值
    private double _max = double.MinValue;          // 最大值


    // ------------------------------------------------------------------------
    // 行为
    // ------------------------------------------------------------------------

    /// <summary>连接设备（真实项目里：打开串口 / 连上 Modbus 从站）</summary>
    public void Connect() { IsConnected = true; }

    /// <summary>断开设备（真实项目里：关闭串口）</summary>
    public void Disconnect() { IsConnected = false; }

    /// <summary>
    /// 写入一次采样值。
    /// 返回 true 表示本次超限报警。
    /// 设备未连接时直接返回 false，并且【不记录任何数据】——这就是"掉线"。
    /// </summary>
    public bool Update(double raw)
    {
        if (!IsConnected)
        {
            return false;          // 掉线：拒绝写入
        }

        _value = raw;
        _readCount++;
        _sum += raw;
        if (raw < _min) { _min = raw; }
        if (raw > _max) { _max = raw; }

        if (IsAlarming())
        {
            _alarmCount++;
            return true;
        }
        return false;
    }

    /// <summary>当前值是否超限</summary>
    public bool IsAlarming()
    {
        return _value < LowerLimit || _value > UpperLimit;
    }

    /// <summary>返回报警类型的中文描述</summary>
    public string AlarmText()
    {
        if (_value > UpperLimit) { return "超上限"; }
        if (_value < LowerLimit) { return "低于下限"; }
        return "正常";
    }


    // ------------------------------------------------------------------------
    // 只读属性：外部只能读，不能改
    //   => 这种写法叫"表达式体属性"，等价于 get { return _value; }
    // ------------------------------------------------------------------------

    public double Value      => _value;
    public int    ReadCount  => _readCount;
    public int    AlarmCount => _alarmCount;

    public double Average => (_readCount == 0) ? 0 : _sum / _readCount;

    // 一次都没采到时，最小/最大值还是初始哨兵值，要特殊处理，否则会显示 ±1.7e308
    public double Min => (_readCount == 0) ? 0 : _min;
    public double Max => (_readCount == 0) ? 0 : _max;
}
