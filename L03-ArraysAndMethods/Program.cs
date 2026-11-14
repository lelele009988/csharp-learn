// ============================================================================
//  第 3 课   数组、集合与方法（把逻辑封装成"子程序"）
//
//  场景：把"算平均值、找最大值、数报警个数"写成方法，之后到处调用
//
//  运行方式：
//      dotnet run --project L03-ArraysAndMethods
// ============================================================================


// ---------------------------------------------------------------------------
// 1. 方法（函数）= 给一段逻辑起个名字，可以反复调用、能传参数、能拿回结果
//
//    相当于 PLC 里的子程序 / FC / FB：
//        - 参数列表  ≈ 子程序的输入引脚
//        - return    ≈ 子程序的输出
//
//    写法：   static 返回类型 方法名(参数类型 参数名, ...) { ... return 结果; }
//    如果不需要返回结果，返回类型写 void
// ---------------------------------------------------------------------------

static double Average(double[] values)
{
    double sum = 0;
    foreach (double v in values)
    {
        sum += v;
    }
    return sum / values.Length;
}

static double Max(double[] values)
{
    double max = values[0];
    foreach (double v in values)
    {
        if (v > max) { max = v; }
    }
    return max;
}

static double Min(double[] values)
{
    double min = values[0];
    foreach (double v in values)
    {
        if (v < min) { min = v; }
    }
    return min;
}

// 带两个参数的方法：第一个是数据，第二个是判断上限
static int CountOver(double[] values, double limit)
{
    int count = 0;
    foreach (double v in values)
    {
        if (v > limit) { count++; }
    }
    return count;
}

// 没有返回值的方法（void）：只做事，不返回结果
static void PrintTitle(string text)
{
    Console.WriteLine($"========== {text} ==========");
}


// ---------------------------------------------------------------------------
// 2. 调用上面这些方法
// ---------------------------------------------------------------------------
double[] temps = { 45.2, 47.8, 51.0, 88.6, 46.3, 92.1, 44.9, 48.2 };

PrintTitle("温度统计");
Console.WriteLine($"  平均值：{Average(temps):F2} ℃");
Console.WriteLine($"  最大值：{Max(temps)} ℃");
Console.WriteLine($"  最小值：{Min(temps)} ℃");
Console.WriteLine($"  超过 85 ℃ 的个数：{CountOver(temps, 85)}");
Console.WriteLine($"  超过 50 ℃ 的个数：{CountOver(temps, 50)}   ← 同一个方法，换个参数就复用");
Console.WriteLine($"  极差（最大 - 最小）：{Max(temps) - Min(temps):F2} ℃   ← 方法可以嵌套使用");


// ---------------------------------------------------------------------------
// 3. List<T>：长度可以变的"动态数组"
//
//    真实上位机里数据是一直往列表里加的，事先不可能知道有多少条，
//    这时就用 List<double> 而不是 double[]
// ---------------------------------------------------------------------------
PrintTitle("List 动态集合");

List<double> history = new List<double>();     // 先建一个空列表

// 模拟设备不断上传数据
double[] incoming = { 44.1, 45.9, 88.8, 46.2 };
foreach (double v in incoming)
{
    history.Add(v);                                              // 往末尾追加一条
    Console.WriteLine($"  采集到 {v} ℃，当前共 {history.Count} 条记录");
}

Console.WriteLine($"  历史数据平均值：{Average(history.ToArray()):F2} ℃");
Console.WriteLine("            （说明：Average 只认数组，所以先把 List 转成数组；");
Console.WriteLine("              下一课学了更通用的写法就不用转了）");

// List 的常用操作
history.Remove(88.8);                                          // 按值删除
Console.WriteLine($"  删掉 88.8 之后剩 {history.Count} 条：{string.Join(", ", history)}");

Console.WriteLine($"  第一条是：{history[0]}");                 // 和数组一样用 [下标] 取值
Console.WriteLine($"  最后一条是：{history[history.Count - 1]}");
Console.WriteLine($"  包含 46.2 吗？{history.Contains(46.2)}");

history.Clear();                                               // 清空
Console.WriteLine($"  清空后剩 {history.Count} 条");


// ---------------------------------------------------------------------------
// 4. 【重要】数组是"引用类型"：把它传给方法，方法里改的就是同一份数据
//
//    这和 int、double 这类"值类型"完全不同，是新手最容易踩的坑之一
// ---------------------------------------------------------------------------
PrintTitle("数组传进方法里会被真的改动");

static void AddOneToFirst(double[] values)
{
    values[0] = values[0] + 1;         // 改的是调用方"那一份"数组
}

double[] data = { 10.0, 20.0 };
Console.WriteLine($"  调用前：{string.Join(", ", data)}");
AddOneToFirst(data);
Console.WriteLine($"  调用后：{string.Join(", ", data)}   ← 原数组被改了！");

// 对比：int 是值类型，传进去改不影响外面
static void TryChangeNumber(int n)
{
    n = 999;                            // 只改了方法内部的副本
}

int number = 5;
TryChangeNumber(number);
Console.WriteLine($"  int 传进方法后：{number}   ← 没变，因为 int 是值类型");


// ---------------------------------------------------------------------------
// 5. 用方法 + 集合做一个"迷你采集统计器"
//    这就是上位机里"采集 → 统计 → 报警"的雏形
// ---------------------------------------------------------------------------
PrintTitle("迷你采集统计器");

double limit = 80;                     // 报警上限
List<double> samples = new List<double>();
double[] simulated = { 62.3, 78.9, 81.5, 64.0, 85.2, 70.1 };

foreach (double v in simulated)
{
    samples.Add(v);
}

int alarmTotal = CountOver(samples.ToArray(), limit);

Console.WriteLine($"  本次采集 {samples.Count} 条，报警上限 {limit} ℃");
Console.WriteLine($"  平均 {Average(samples.ToArray()):F2} ℃，最高 {Max(samples.ToArray())} ℃，最低 {Min(samples.ToArray())} ℃");
Console.WriteLine($"  超限 {alarmTotal} 条");

if (alarmTotal > 0)
{
    Console.WriteLine("  → 需要弹出报警提示");
}
else
{
    Console.WriteLine("  → 一切正常");
}

Console.WriteLine();
Console.WriteLine("第 3 课结束。");
Console.WriteLine("练习：给 CountOver 再传一个下限参数，写一个 CountBetween(values, low, high) 方法。");
