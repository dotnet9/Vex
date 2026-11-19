namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>代码块下载的建议文件名：优先用围栏信息里的路径，否则按语言给扩展名。</summary>
internal static class MarkdownCodeFileNames
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs"] = "cs", ["csharp"] = "cs", ["c#"] = "cs", ["fs"] = "fs", ["fsharp"] = "fs", ["vb"] = "vb",
        ["js"] = "js", ["javascript"] = "js", ["jsx"] = "jsx", ["ts"] = "ts", ["typescript"] = "ts", ["tsx"] = "tsx",
        ["py"] = "py", ["python"] = "py", ["rb"] = "rb", ["ruby"] = "rb", ["go"] = "go", ["rust"] = "rs", ["rs"] = "rs",
        ["java"] = "java", ["kotlin"] = "kt", ["kt"] = "kt", ["swift"] = "swift", ["c"] = "c", ["cpp"] = "cpp", ["c++"] = "cpp",
        ["h"] = "h", ["hpp"] = "hpp", ["php"] = "php", ["lua"] = "lua", ["r"] = "r", ["dart"] = "dart", ["scala"] = "scala",
        ["sh"] = "sh", ["bash"] = "sh", ["zsh"] = "sh", ["shell"] = "sh", ["powershell"] = "ps1", ["ps1"] = "ps1", ["bat"] = "bat",
        ["html"] = "html", ["xml"] = "xml", ["xaml"] = "xaml", ["axaml"] = "axaml", ["css"] = "css", ["scss"] = "scss",
        ["json"] = "json", ["jsonc"] = "jsonc", ["yaml"] = "yaml", ["yml"] = "yml", ["toml"] = "toml", ["ini"] = "ini",
        ["sql"] = "sql", ["md"] = "md", ["markdown"] = "md", ["diff"] = "diff", ["patch"] = "patch", ["dockerfile"] = "Dockerfile",
        ["makefile"] = "mk", ["graphql"] = "graphql", ["proto"] = "proto", ["vue"] = "vue", ["svelte"] = "svelte", ["tex"] = "tex",
        ["latex"] = "tex", ["mermaid"] = "mmd", ["csv"] = "csv", ["txt"] = "txt", ["text"] = "txt",
    };

    public static string Suggest(string? language, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var name = global::System.IO.Path.GetFileName(path.Replace('\\', '/'));
            if (name.Length > 0)
            {
                return name;
            }
        }

        if (language is not null && Extensions.TryGetValue(language, out var extension))
        {
            return extension == "Dockerfile" ? extension : "snippet." + extension;
        }

        return "snippet.txt";
    }
}
