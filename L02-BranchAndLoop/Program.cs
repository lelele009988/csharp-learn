// ============================================================================
//  第 2 课   分支与循环
//
//  场景：根据温度判断要不要报警 + 遍历一批采集到的数据做统计
//
//  运行方式：
//      dotnet run --project L02-BranchAndLoop
// ============================================================================


// ---------------------------------------------------------------------------
// 1. if / else if / else —— 根据条件走不同分支
//
//    相当于梯形图里的比较指令：>  <  >=  <=  ==  !=
//    【新手最容易错的地方】判断"相等"要用两个等号 == ，
//    一个等号 = 是"赋值"（把右边的值放进左边的变量）
// ---------------------------------------------------------------------------
Console.WriteLine("========== 1. 温度报警判断 ==========");

double temperature = 87.3;

if (temperature >= 85)
{
    Console.WriteLine($"温度 {temperature} ℃ → 【报警】超过上限 85 ℃");
}
else if (temperature >= 70)
{
    Console.WriteLine($"温度 {temperature} ℃ → 【警告】偏高，注意观察");
}
else
{
    Console.WriteLine($"温度 {temperature} ℃ → 正常");
}


// ---------------------------------------------------------------------------
// 2. 逻辑运算： && 而且    || 或者    ! 不（取反）
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 2. 组合条件 ==========");

double pressure = 0.42;
bool running = true;
bool emergencyStop = false;

if (running && !emergencyStop && pressure > 0.5)
{
    Console.WriteLine("运行中 + 无急停 + 压力正常 → 允许执行下一个动作");
}
else if (emergencyStop)
{
    Console.WriteLine("急停被按下 → 禁止所有动作");
}
else
{
    Console.WriteLine("条件不满足 → 保持等待");
}


// ---------------------------------------------------------------------------
// 3. 数组 + for 循环
//
//    【关键差异】C# 的数组下标从 0 开始，不是从 1 开始！
//    一定要记住：第一个元素是 [0]，最后一个元素是 [长度 - 1]
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 3. 遍历采集数据（for）==========");

double[] temps = { 45.2, 47.8, 51.0, 88.6, 46.3, 92.1, 44.9, 48.2 };
//                  [0]   [1]   [2]   [3]   [4]   [5]   [6]   [7]

Console.WriteLine($"共采集 {temps.Length} 个温度值，下标范围 0 ~ {temps.Length - 1}");

for (int i = 0; i < temps.Length; i++)
{
    string mark = "";
    if (temps[i] >= 85)
    {
        mark = "   ← 超限！";
    }
    Console.WriteLine($"  第 {i + 1} 路：{temps[i]} ℃{mark}");
}


// ---------------------------------------------------------------------------
// 4. foreach —— 不需要下标，只是"把每个元素挨个看一遍"
//    只关心"每一个元素的值"时，foreach 比 for 更简洁、更不容易写错
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 4. foreach 遍历 ==========");

foreach (double t in temps)
{
    if (t >= 85)
    {
        Console.WriteLine($"  发现超限值：{t} ℃");
    }
}


// ---------------------------------------------------------------------------
// 5. 循环累加 / 计数 / 找最大值 —— 循环最典型的用法
//    这几个"套路"在数据采集程序里几乎天天用
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 5. 累加、计数、找最大值 ==========");

double sum = 0;
int alarmCount = 0;
double max = temps[0];              // 先假设第一个就是最大的

foreach (double t in temps)
{
    sum += t;                       // 累加
    if (t >= 85) { alarmCount++; }  // 计数（++ 就是"加 1"）
    if (t > max) { max = t; }       // 遇到更大的就换掉
}

Console.WriteLine($"  总和：    {sum}");
Console.WriteLine($"  平均值：  {sum / temps.Length:F2} ℃");
Console.WriteLine($"  最大值：  {max} ℃");
Console.WriteLine($"  超限个数：{alarmCount}");


// ---------------------------------------------------------------------------
// 6. while 循环：条件成立就一直循环
//    这里模拟"轮询等待设备就绪"——真实上位机里就是反复读一个状态位
//    【注意】while 里一定要有能改变条件的语句，否则会死循环
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 6. while 模拟轮询等待就绪 ==========");

int attempt = 0;
bool ready = false;

while (!ready)
{
    attempt++;
    Console.WriteLine($"  第 {attempt} 次查询设备状态……");

    if (attempt >= 3)     // 模拟第 3 次时设备就绪
    {
        ready = true;
    }
}
Console.WriteLine("  设备就绪 → 开始采集数据");


// ---------------------------------------------------------------------------
// 7. switch：按"状态码"选分支，比一长串 if-else 更清楚
//    工控里常见约定：0 = 停机  1 = 运行  2 = 报警  3 = 维护
//    每个 case 结束都要写 break，表示"到此为止，不再往下走"
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 7. switch 状态码判断 ==========");

int stateCode = 2;

switch (stateCode)
{
    case 0:
        Console.WriteLine("  状态：停机");
        break;
    case 1:
        Console.WriteLine("  状态：运行");
        break;
    case 2:
        Console.WriteLine("  状态：报警 → 请检查报警列表");
        break;
    case 3:
        Console.WriteLine("  状态：维护");
        break;
    default:                                   // 以上都没匹配上时走这里
        Console.WriteLine($"  状态：未知（状态码 {stateCode}）");
        break;
}


// ---------------------------------------------------------------------------
// 8. 二维数组 + 循环套循环：遍历"多台设备 × 每台多路温度"
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 8. 二维数组（多台设备）==========");

double[,] allTemps =
{
    { 45.2, 47.8, 88.6 },     // 设备 1 的三路温度
    { 44.1, 92.3, 46.0 },     // 设备 2 的三路温度
};

int devCount = allTemps.GetLength(0);      // 第 0 维长度 = 设备台数
int chCount = allTemps.GetLength(1);       // 第 1 维长度 = 每台的路数

for (int dev = 0; dev < devCount; dev++)
{
    for (int ch = 0; ch < chCount; ch++)
    {
        double t = allTemps[dev, ch];
        string mark = t >= 85 ? "  ← 超限！" : "";
        Console.WriteLine($"  设备{dev + 1} 第{ch + 1}路：{t} ℃{mark}");
    }
}

Console.WriteLine();
Console.WriteLine("第 2 课结束。");
Console.WriteLine("练习：把 temps 数组改成你自己的 8 路数据，并把报警上限从 85 改成你要的值。");
