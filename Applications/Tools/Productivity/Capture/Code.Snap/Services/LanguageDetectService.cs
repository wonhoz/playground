namespace CodeSnap.Services;

public static class LanguageDetectService
{
    private static readonly (string Lang, string[] Keywords)[] Rules =
    [
        ("Python",     ["def ", "import ", "print(", "elif ", "elif:", "self.", "__init__", "None:", "True:", "False:"]),
        ("TypeScript", ["interface ", ": string", ": number", "type ", "enum ", ": boolean", "readonly ", "<T>", "?: "]),
        ("JavaScript", ["const ", "let ", "=>", "function ", "console.log", "require(", "module.exports", "async ", "await "]),
        ("C#",         ["namespace ", "using System", "var ", "public class", "Console.", "string[] ", "async Task", "private ", "protected "]),
        ("Java",       ["package ", "public class", "System.out.", "import java", "void ", "ArrayList<", "HashMap<", "throws "]),
        ("HTML",       ["<html", "<div", "<!DOCTYPE", "<body", "<head", "<script", "<style", "<p>", "<a "]),
        ("CSS",        ["margin:", "padding:", "border:", "display:", "color:", "font-size:", "background:", "position:"]),
        ("SQL",        ["SELECT ", "FROM ", "WHERE ", "INSERT INTO", "UPDATE ", "DELETE ", "CREATE TABLE", "JOIN ", "GROUP BY"]),
        ("JSON",       ["{\"", "\":", "[{", "null,", "true,", "false,", "},", "},"]),
        ("XML",        ["<?xml", "xmlns", "<root>", "</", "/>", "<!-- "]),
        ("Ruby",       ["def ", "end\n", "require ", "class ", "puts ", "attr_", "@", "do |", "render "]),
        ("Go",         ["func ", "package main", "import (", "fmt.", "var ", ":=", "goroutine", "chan ", "defer "]),
        ("Rust",       ["fn ", "let mut", "impl ", "use std::", "match ", "enum ", "struct ", "pub fn", "-> Result"]),
        ("PHP",        ["<?php", "echo ", "$_", "function ", "->", "namespace ", "use ", "class ", "public function"]),
        ("Markdown",   ["# ", "## ", "### ", "**", "__", "- [", "```", "[](", "> "]),
    ];

    public static string Detect(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Text";

        var scores = new Dictionary<string, int>();
        foreach (var (lang, keywords) in Rules)
        {
            int score = keywords.Count(k => code.Contains(k, StringComparison.Ordinal));
            if (score > 0) scores[lang] = score;
        }

        if (scores.Count == 0) return "Text";

        return scores.OrderByDescending(kv => kv.Value).First().Key;
    }
}
