// ============================================================================
//  第 1 课   变量、数据类型、算术运算、屏幕输出
//
//  场景：把"从 PLC 读到的几个数据"存进变量，做点计算，再打印出来
//
//  运行方式（你在普通 PowerShell 里）：
//      cd C:\Users\admin\Documents\deepseek-harness\default-workspace\csharp-learn
//      dotnet run --project L01-Variables
// ============================================================================


// ---------------------------------------------------------------------------
// 1. 变量 = 给一个数据起个名字，把它存在内存里
//
//    C# 是"强类型"语言：每个变量都必须先说明它装的是什么类型的数据。
//    写法固定是：   类型 名字 = 初值;
// ---------------------------------------------------------------------------

// int    整数（不带小数点）—— 相当于 PLC 里的 16 位 / 32 位整型寄存器
int speed = 1450;              // 电机转速，单位 rpm
int axisCount = 3;             // 伺服轴数量

// double 小数（带小数点）—— 温度、电压、压力这类模拟量都用 double
double temperature = 62.5;     // 温度，单位 ℃
double voltage = 24.0;         // 电源电压，单位 V

// string 文本 —— 用双引号包起来，可以放中文
string deviceName = "注液机-01";
string plcIp = "192.168.1.10";

// bool   只有两个值：true（真 / 是）或 false（假 / 否）—— 相当于一个 M 位
bool isRunning = true;
bool hasAlarm = false;


// ---------------------------------------------------------------------------
// 2. 输出到屏幕
//
//    $"...{变量}..." 叫"字符串插值"，是把变量嵌进文本里最方便的写法
// ---------------------------------------------------------------------------
Console.WriteLine("========== 设备状态 ==========");
Console.WriteLine($"设备名称：{deviceName}");
Console.WriteLine($"PLC 地址：{plcIp}");
Console.WriteLine($"转速：{speed} rpm");
Console.WriteLine($"轴数：{axisCount}");
Console.WriteLine($"温度：{temperature} ℃");
Console.WriteLine($"电压：{voltage} V");
Console.WriteLine($"运行中：{isRunning}");
Console.WriteLine($"有报警：{hasAlarm}");


// ---------------------------------------------------------------------------
// 3. 算术运算： +  -  *  /  %
//    % 叫"取余数"，常用来判断能不能整除
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 算术运算 ==========");

// 转速换算成"每秒转多少圈"
double perSecond = speed / 60.0;
Console.WriteLine($"1450 rpm = 每秒 {perSecond} 转");

// 摄氏转华氏：F = C * 9 / 5 + 32
double fahrenheit = temperature * 9 / 5 + 32;
Console.WriteLine($"{temperature} ℃ = {fahrenheit} ℉");

// 取余数的用法：17 个产品，每箱装 5 个
int total = 17;
int perBox = 5;
Console.WriteLine($"{total} 个产品，每箱 {perBox} 个 → 装满 {total / perBox} 箱，剩 {total % perBox} 个");


// ---------------------------------------------------------------------------
// 4. 【重要陷阱】整数相除会丢掉小数部分！
//
//    这跟 PLC 里用整数寄存器做除法是同一个坑，务必记住。
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 整数除法的坑 ==========");

int a = 7;
int b = 2;
Console.WriteLine($"7 / 2    = {a / b}       ← 两边都是整数，小数被直接丢掉");
Console.WriteLine($"7.0 / 2  = {7.0 / b}     ← 只要有一个是小数，结果就是小数");
Console.WriteLine($"7 / 2.0  = {a / 2.0}");


// ---------------------------------------------------------------------------
// 5. 变量的值可以改 —— 这就是它叫"变量"的原因
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 改变变量的值 ==========");

Console.WriteLine($"修改前 speed = {speed}");
speed = 1500;                  // 直接赋一个新值
Console.WriteLine($"改成 1500    = {speed}");
speed = speed + 50;            // 在原来的基础上加 50
Console.WriteLine($"再加上 50    = {speed}");
speed += 100;                  // += 是"在原有基础上加"的简写，等价于 speed = speed + 100
Console.WriteLine($"再加 100     = {speed}");


// ---------------------------------------------------------------------------
// 6. 类型转换
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 类型转换 ==========");

double measured = 23.87;

int truncated = (int)measured;                 // (int) 是强制转换：直接砍掉小数，不四舍五入
Console.WriteLine($"{measured} 转成 int     = {truncated}    ← 是砍掉，不是四舍五入");

int rounded = (int)Math.Round(measured);       // 想四舍五入就用 Math.Round
Console.WriteLine($"{measured} 四舍五入后   = {rounded}");


// ---------------------------------------------------------------------------
// 7. 小数保留位数的写法（做报表时常用）
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("========== 控制小数位数 ==========");

double value = 3.14159265;
Console.WriteLine($"不处理        ：{value}");
Console.WriteLine($"保留 2 位小数 ：{value:F2}");     // F2 = 定点数，2 位小数
Console.WriteLine($"保留 4 位小数 ：{value:F4}");
Console.WriteLine($"百分数形式    ：{0.8735:P1}");    // P1 = 百分数，1 位小数

// 顺便解释一个新手常困惑的点：为什么上面"电压 24.0"打印出来是 24 ？
// 因为 double 原样输出时，末尾无意义的 0 不显示。想显示成 24.0，就要指定小数位：
Console.WriteLine($"电压原样输出  ：{voltage}      ← 末尾的 .0 不显示");
Console.WriteLine($"电压保留 1 位 ：{voltage:F1}    ← 这样才是 24.0");


Console.WriteLine();
Console.WriteLine("第 1 课结束。");
Console.WriteLine("练习：把上面的数字改成你自己的设备参数，再运行一次看输出变化。");
