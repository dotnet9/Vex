# C# 语言之美

![C# 类型体系封面](图片/csharp-cover.svg)

[TOC]

1999 年 1 月，Anders Hejlsberg——Turbo Pascal 与 Delphi 的作者——在微软组建了一个新语言团队，项目代号 **Cool**（C-like Object Oriented Language）。2000 年 6 月随 .NET 计划公开时更名为 C#，2002 年随 Visual Studio .NET 发布 1.0，同年 12 月被 ECMA 接纳为标准 **ECMA-334**。

# 号并非 ++ 的歪写，而是乐谱中的升音符：四枚加号合在一处，暗示它站在 C 系语言肩膀上又高了一度。

## 版本时间线上的四次跃迁

- **C# 2.0（2005）**：泛型。集合从 `ArrayList` 的装箱时代进入 `List<T>`。
- **C# 3.0（2007）**：LINQ、Lambda 与扩展方法，把查询写进语言。
- **C# 5.0（2012）**：`async/await`，异步代码第一次读起来像同步。
- **C# 9–12（2020–2023）**：record、模式匹配成熟、集合表达式，语言重新变得年轻。

完整版本史见 [C# 版本历史 - Microsoft Learn](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-version-history)。

## 类型即契约：record 与不可变性

面向对象教我们把状态藏起来，再用属性与方法暴露行为；`record` 则反其道而行——把状态摆在明面上，让「值」本身成为契约。

```csharp
public sealed record Point3D(double X, double Y, double Z)
{
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
}

var origin = new Point3D(0, 0, 0);
var shifted = origin with { Z = 4.2 };

Console.WriteLine(shifted == origin); // False：按值比较，而非引用
Console.WriteLine(shifted);           // Point3D { X = 0, Y = 0, Z = 4.2 }
```

`with` 表达式生成的是**副本**而非突变：旧值安然无恙，新值随心定制。在多线程与撤销/重做这类场景里，「不可变 + 按值相等」消灭了一整类缺陷。

## 模式匹配：把 if-else 山拆成决策表

C# 9 的关系与逻辑模式让复杂分派逻辑可以直接写成一张表，读代码的顺序就是决策的顺序。

```csharp
static string Describe(Shape shape) => shape switch
{
    Circle { Radius: > 100 } c  => $"大圆，半径 {c.Radius}",
    Circle c                    => $"圆，半径 {c.Radius}",
    Rect { W: var w, H: var h } when w == h => $"正方形，边长 {w}",
    Rect r                      => $"矩形 {r.W}×{r.H}",
    null                        => "空形状",
    _                           => "未知"
};
```

编译器会做**穷尽性检查**：新增一个 `Shape` 子类而漏写分支，它会当场指出。

## 集合表达式：初始化的终极形态

C# 12 起，`[..]` 可以为任何集合类型赋值——编译器负责选择最优构造路径：

```csharp
int[]     primes = [2, 3, 5, 7, 11];
List<int> evens  = [.. primes, 13, 17];   // 展开运算符拼接
int[][]   grid   = [[1, 0], [0, 1]];      // 嵌套同样自然
```

## LINQ：把循环写成句子

LINQ 的价值不在少打几个字，而在**把意图从机制里剥离**：

```csharp
var hotspots = files
    .Where(f => f.Lines > 300)
    .OrderByDescending(f => f.Commits)
    .Select(f => $"{f.Path}（{f.Commits} 次提交）")
    .Take(5)
    .ToList();
```

声明「要什么」，而不是「怎么遍历」；延迟执行让组合保持零成本。

---

> 语言特性是工具，美感来自组合：类型说清数据长什么样，模式匹配说清它怎么分派，LINQ 说清它从哪来。

延伸阅读：[C# 文档首页](https://learn.microsoft.com/dotnet/csharp/) · [设计准则](https://learn.microsoft.com/dotnet/standard/design-guidelines/)
