// netstandard2.0 目标下 record 语法需要 IsExternalInit 类型定义（编译器内置类型的手动桥接）。
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
