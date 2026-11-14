// ============================================================================
//  哥德巴赫猜想：计算验证 + 与 Hardy-Littlewood 预测公式对比
//
//  【重要说明：有序 vs 无序】
//    Hardy-Littlewood 渐近公式数的是"有序对"：N = p + q 中 (p,q) 与 (q,p) 算两种。
//    本程序内部用 p <= q 统计（无序），在需要与公式比较时再转成有序：
//        有序个数 = 2 * 无序个数 - [N/2 是素数 ? 1 : 0]
//    （因为 p = q = N/2 那一项在有序计数里只出现一次）
// ============================================================================

const int LIMIT = 1_000_000;

// ---------------------------------------------------------------------------
// 1. 埃拉托斯特尼筛法
// ---------------------------------------------------------------------------
var isPrime = new bool[LIMIT + 1];
Array.Fill(isPrime, true);
isPrime[0] = false;
isPrime[1] = false;
for (int i = 2; (long)i * i <= LIMIT; i++)
{
    if (!isPrime[i]) continue;
    for (int j = i * i; j <= LIMIT; j += i) isPrime[j] = false;
}

var primes = new List<int>();
for (int i = 2; i <= LIMIT; i++) if (isPrime[i]) primes.Add(i);
Console.WriteLine($"筛法范围: 2 ~ {LIMIT:N0}    素数个数 pi(N) = {primes.Count:N0}");

// ---------------------------------------------------------------------------
// 2. 统计无序表示个数 R(N)：N = p + q 且 p <= q 的方法数
// ---------------------------------------------------------------------------
var R = new int[LIMIT + 1];
var sw = System.Diagnostics.Stopwatch.StartNew();
for (int i = 0; i < primes.Count; i++)
{
    int p = primes[i];
    if (2 * p > LIMIT) break;
    for (int j = i; j < primes.Count; j++)
    {
        int s = p + primes[j];
        if (s > LIMIT) break;
        R[s]++;
    }
}
sw.Stop();
Console.WriteLine($"枚举素数对完成，用时 {sw.Elapsed.TotalSeconds:F1} 秒");

// 有序个数 = 2 * 无序 - (N/2 是否为素数)
int Ordered(int n) => 2 * R[n] - (isPrime[n / 2] ? 1 : 0);

// ---------------------------------------------------------------------------
// 3. 验证：4 ~ LIMIT 的所有偶数
// ---------------------------------------------------------------------------
int evenCount = 0, failures = 0, maxR = 0, argmaxR = 0;
for (int n = 4; n <= LIMIT; n += 2)
{
    evenCount++;
    if (R[n] == 0) failures++;
    if (R[n] > maxR) { maxR = R[n]; argmaxR = n; }
}
Console.WriteLine();
Console.WriteLine("=== 1. 验证结果 ===");
Console.WriteLine($"检查偶数个数        : {evenCount:N0}   (4 ~ {LIMIT:N0})");
Console.WriteLine($"没有找到表示的偶数  : {failures}");
Console.WriteLine(failures == 0
    ? "结论                : 此范围内哥德巴赫猜想全部成立"
    : "结论                : 发现反例！");
Console.WriteLine($"表示个数最多的偶数  : N = {argmaxR:N0}，共 {maxR:N0} 种表示（无序）");

// ---------------------------------------------------------------------------
// 4. Hardy-Littlewood 预测
//    R_ordered(N) ~ 2*C2 * prod_{p|N, p>2} (p-1)/(p-2) * N/(ln N)^2
// ---------------------------------------------------------------------------
const double C2 = 0.66016181584686957;

double SingularFactor(int n)
{
    double f = 1.0;
    int m = n;
    foreach (int p in primes)
    {
        if ((long)p * p > m) break;
        if (m % p != 0) continue;
        while (m % p == 0) m /= p;
        if (p > 2) f *= (double)(p - 1) / (p - 2);
    }
    if (m > 2) f *= (double)(m - 1) / (m - 2);   // 剩下的大素因子
    return f;
}

double PredictOrdered(int n) => 2 * C2 * SingularFactor(n) * n / Math.Pow(Math.Log(n), 2);

Console.WriteLine();
Console.WriteLine("=== 2. 实测（有序）与 Hardy-Littlewood 预测对比 ===");
Console.WriteLine("       N      无序R(N)    有序个数     预测(有序)   实测/预测");
foreach (int n in new[] { 100, 1_000, 10_000, 100_000, 1_000_000 })
{
    double pred = PredictOrdered(n);
    Console.WriteLine($"{n,10:N0}  {R[n],10:N0}  {Ordered(n),11:N0}  {pred,12:N0}  {Ordered(n) / pred,11:F3}");
}
Console.WriteLine("      （比值从 1.4 逐步收敛到 1 —— 这是渐近公式的正常误差，不是公式错了）");

// ---------------------------------------------------------------------------
// 5. "哥德巴赫彗星"的分层结构
//    归一化量 S(N) = R_ordered(N) * (ln N)^2 / N
//    按 N 的奇素因子签名分组（只用 <= 13 的素因子，且要求没有更大的奇素因子）
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 3. 归一化后的分层（彗星条纹的来源）===");
Console.WriteLine("   范围: N 在 [100000, 1000000]，且 N 的奇数部分只含素因子 <= 13");
Console.WriteLine();
Console.WriteLine("   N 的奇素因子签名     样本数    实测平均S(N)   预测S(N)    相对误差");

int[] smallOdd = { 3, 5, 7, 11, 13 };
var groups = new Dictionary<string, (double predFactor, List<int> ns)>();

for (int n = 100_000; n <= LIMIT; n += 2)
{
    int m = n;
    while (m % 2 == 0) m /= 2;
    var sig = new List<int>();
    foreach (int p in smallOdd)
    {
        if (m % p == 0)
        {
            sig.Add(p);
            while (m % p == 0) m /= p;
        }
    }
    if (m > 1) continue;                      // 还有 > 13 的奇素因子，跳过

    string key = sig.Count == 0 ? "(只有 2)" : string.Join(" x ", sig);
    double pf = 1.0;
    foreach (int p in sig) pf *= (double)(p - 1) / (p - 2);

    if (!groups.TryGetValue(key, out var g)) g = (pf, new List<int>());
    g.ns.Add(n);
    groups[key] = g;
}

foreach (var kv in groups.OrderByDescending(k => k.Value.ns.Count))
{
    double predS = 2 * C2 * kv.Value.predFactor;
    double sum = 0;
    foreach (int n in kv.Value.ns)
        sum += (double)Ordered(n) * Math.Pow(Math.Log(n), 2) / n;
    double avgS = sum / kv.Value.ns.Count;
    double err = (avgS - predS) / predS * 100;
    Console.WriteLine($"   {kv.Key,-20} {kv.Value.ns.Count,6:N0}   {avgS,11:F4}   {predS,9:F4}   {err,7:F1}%");
}

// ---------------------------------------------------------------------------
// 6. 让"两个素数之和"看得见
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 4. 具体例子（N = 1,000,000 的前 6 种表示）===");
int shown = 0;
for (int p = 2; p <= 500_000 && shown < 6; p++)
{
    if (!isPrime[p]) continue;
    int q = 1_000_000 - p;
    if (q >= p && isPrime[q]) { Console.WriteLine($"   1,000,000 = {p:N0} + {q:N0}"); shown++; }
}
Console.WriteLine($"   （共 {R[1_000_000]:N0} 种表示）");

Console.WriteLine();
Console.WriteLine("★ 以上全部是【有限范围内的验证】，不是证明。");
Console.WriteLine("  目前人类已验证到 4 x 10^18，但偶数有无穷多个。");
