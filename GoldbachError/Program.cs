// ============================================================================
//  哥德巴赫：所谓"主项误差"到底有没有结构？
//
//  背景：实测 R(N) 比粗糙主项 2*C2*S(N)*N/(ln N)^2 系统性高约 18~19%。
//
//  猜想：这 19% 不是"误差"，而是可以精确算出的次主项。
//        把粗糙的 1/ln 密度换成"对真实素数求和"，即
//            sum_{q <= N-2, q 素数} 1/ln q
//        它与 N/(ln N)^2 的比值有渐近展开
//            1 + 2/lnN + 6/ln^2N + 24/ln^3N + ...
//        在 N=10^6 时约 1.19 —— 正好是观测值。
//
//  程序做五件事：
//    1) 两个模型对比
//    2) 验证次主项展开与 D/A 的吻合程度
//    3) 剥离主项后，看残余 E(N) 的尺度是否 ~ sqrt(N)
//    4) 看 E/sqrt(N) 的分布像不像噪声
//    5) 看残余里还有没有算术结构
// ============================================================================

const int LIMIT = 2_000_000;
const double C2 = 0.66016181584686957;

// ---------------------------------------------------------------------------
// 1. 筛法 + 最小素因子表
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

var spf = new int[LIMIT + 1];
for (int i = 2; i <= LIMIT; i++)
{
    if (spf[i] != 0) continue;
    for (long j = i; j <= LIMIT; j += i)
        if (spf[j] == 0) spf[j] = i;
}

var primes = new List<int>();
for (int i = 2; i <= LIMIT; i++) if (isPrime[i]) primes.Add(i);
Console.WriteLine($"范围 2 ~ {LIMIT:N0}    pi(N) = {primes.Count:N0}");

// ---------------------------------------------------------------------------
// 2. 统计表示个数（先按无序 p<=q 统计，再转有序）
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
Console.WriteLine($"枚举素数对用时 {sw.Elapsed.TotalSeconds:F1} 秒");

int Ordered(int n) => 2 * R[n] - (isPrime[n / 2] ? 1 : 0);

// ---------------------------------------------------------------------------
// 3. 奇异级数因子 S(N) = prod_{p|N, p>2} (p-1)/(p-2)
// ---------------------------------------------------------------------------
double Sing(int n)
{
    double f = 1.0;
    while (n > 1)
    {
        int p = spf[n];
        while (n % p == 0) n /= p;
        if (p > 2) f *= (double)(p - 1) / (p - 2);
    }
    return f;
}

// ---------------------------------------------------------------------------
// 4. 前缀和 pref[x] = sum_{q<=x, q 素数} 1/ln q
// ---------------------------------------------------------------------------
var pref = new double[LIMIT + 1];
double acc = 0;
for (int x = 2; x <= LIMIT; x++)
{
    if (isPrime[x]) acc += 1.0 / Math.Log(x);
    pref[x] = acc;
}

double ModelA(int n) => 2 * C2 * Sing(n) * n / Math.Pow(Math.Log(n), 2);
double ModelD(int n) => 2 * C2 * Sing(n) * pref[n - 2];

// ---------------------------------------------------------------------------
// 5. 样本对比
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 1. 两个模型对比 ===");
Console.WriteLine("         N     有序R(N)        模型A          模型D      R/A      R/D");
foreach (int n in new[] { 10_000, 100_000, 500_000, 1_000_000, 2_000_000 })
{
    double a = ModelA(n), d = ModelD(n);
    Console.WriteLine($"{n,10:N0}  {Ordered(n),10:N0}  {a,12:N0}  {d,12:N0}  {Ordered(n) / a,7:F3}  {Ordered(n) / d,7:F3}");
}

// ---------------------------------------------------------------------------
// 6. 次主项展开检验
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 2. 那 19% 是什么：次主项展开 ===");
Console.WriteLine("         N     D/A 实测值    展开式预测      相对差");
foreach (int n in new[] { 100_000, 500_000, 1_000_000, 2_000_000 })
{
    double L = Math.Log(n);
    double ratio = pref[n - 2] / (n / (L * L));
    // 展开式: 1 + 2/L + 6/L^2 + 24/L^3 + ...  → 第 k 项系数是 (k+1)!
    double expand = 1.0, term = 1.0, fact = 1.0;
    for (int k = 1; k <= 10; k++) { fact *= (k + 1); term *= L; expand += fact / term; }
    Console.WriteLine($"{n,10:N0}  {ratio,12:F5}  {expand,13:F5}  {(ratio - expand) / expand * 100,10:F4}%");
}

// ---------------------------------------------------------------------------
// 7. 全范围统计与残余误差
// ---------------------------------------------------------------------------
const int FROM = 100_000;
int cnt = 0;
double sumA = 0, sumD = 0, sumE = 0, sumE2 = 0, sumAbsE = 0;
double eMin = double.MaxValue, eMax = double.MinValue, eAbsMax = 0, argAbsMax = 0;
double rMin = double.MaxValue, rMax = double.MinValue;
var ratios = new List<double>();

for (int n = FROM; n <= LIMIT; n += 2)
{
    double a = ModelA(n), d = ModelD(n);
    double e = Ordered(n) - d;
    double r = e / Math.Sqrt(n);

    cnt++;
    sumA += Ordered(n) / a;
    sumD += Ordered(n) / d;
    sumE += e;
    sumE2 += e * e;
    sumAbsE += Math.Abs(e);
    if (e < eMin) eMin = e;
    if (e > eMax) eMax = e;
    if (Math.Abs(e) > Math.Abs(eAbsMax)) { eAbsMax = Math.Abs(e); argAbsMax = n; }
    if (r < rMin) rMin = r;
    if (r > rMax) rMax = r;
    ratios.Add(r);
}

double meanA = sumA / cnt, meanD = sumD / cnt;
double meanE = sumE / cnt;
double stdE = Math.Sqrt(sumE2 / cnt - meanE * meanE);
double meanR = ratios.Average();
double stdR = Math.Sqrt(ratios.Sum(x => (x - meanR) * (x - meanR)) / ratios.Count);

Console.WriteLine();
Console.WriteLine($"=== 3. 全范围统计（N 从 {FROM:N0} 到 {LIMIT:N0}，共 {cnt:N0} 个偶数）===");
Console.WriteLine($"  模型A（粗糙主项）  平均 R/A = {meanA:F5}    ← 系统性偏高 {(meanA - 1) * 100:F2}%");
Console.WriteLine($"  模型D（素数求和）  平均 R/D = {meanD:F5}    ← 偏离 1 仅 {(meanD - 1) * 100:F3}%");
Console.WriteLine();
Console.WriteLine("  残余  E(N) = R(N) - ModelD(N)");
Console.WriteLine($"    平均 E          = {meanE,12:F1}");
Console.WriteLine($"    标准差          = {stdE,12:F1}");
Console.WriteLine($"    平均 |E|        = {sumAbsE / cnt,12:F1}");
Console.WriteLine($"    |E| 最大值      = {eAbsMax,12:F1}   (在 N = {argAbsMax:N0})");
Console.WriteLine();
Console.WriteLine("  归一化残余  E/sqrt(N)");
Console.WriteLine($"    均值            = {meanR,12:F4}");
Console.WriteLine($"    标准差          = {stdR,12:F4}");
Console.WriteLine($"    范围            = [{rMin:F3}, {rMax:F3}]");

// ---------------------------------------------------------------------------
// 8. E/sqrt(N) 的分布
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 4. 归一化残余 E/sqrt(N) 的分布 ===");
int bins = 12;
var hist = new int[bins];
foreach (double r in ratios)
{
    int b = (int)((r - rMin) / (rMax - rMin) * bins);
    if (b >= bins) b = bins - 1;
    if (b < 0) b = 0;
    hist[b]++;
}
int hmax = hist.Max();
for (int b = 0; b < bins; b++)
{
    double left = rMin + (rMax - rMin) * b / bins;
    double right = rMin + (rMax - rMin) * (b + 1) / bins;
    int barLen = (int)(hist[b] * 60.0 / hmax);
    Console.WriteLine($"  [{left,7:F2}, {right,7:F2})  {hist[b],8:N0}  {new string('#', barLen)}");
}

// ---------------------------------------------------------------------------
// 9. 残余里还有没有算术结构
// ---------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("=== 5. 残余里还有算术结构吗（分块平均，抗相关性）===");
Console.WriteLine("    相邻 N 的 E(N) 并不独立，所以用分块平均估误差棒（64 块）");
const int BLOCKS = 64;
int span = (LIMIT - FROM) / 2 + 1;
foreach (int q in new[] { 3, 5, 7 })
{
    Console.WriteLine($"  N mod {q}:");
    for (int a = 0; a < q; a++)
    {
        var blockMeans = new List<double>();
        for (int b = 0; b < BLOCKS; b++)
        {
            int lo = FROM + 2 * (int)((long)b * span / BLOCKS);
            int hi = FROM + 2 * (int)((long)(b + 1) * span / BLOCKS);
            int c = 0; double s = 0;
            for (int n = lo; n < hi; n += 2)
            {
                if (n % q != a) continue;
                c++;
                s += (Ordered(n) - ModelD(n)) / ModelA(n);
            }
            if (c > 0) blockMeans.Add(s / c * 100);
        }
        double m = blockMeans.Average();
        double var = blockMeans.Sum(x => (x - m) * (x - m)) / (blockMeans.Count - 1);
        double se = Math.Sqrt(var / blockMeans.Count);
        Console.WriteLine($"      [{a}]   {m,8:F3}%  +/- {se:F3}%    (块数 {blockMeans.Count})");
    }
}

Console.WriteLine();
Console.WriteLine("★ 以上全部是对已知启发式的【数值检验】，不是新数学，更不是证明。");
