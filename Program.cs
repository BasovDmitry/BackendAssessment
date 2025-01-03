﻿using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;

class Program
{
    private static readonly string GitHubApiUrl = "https://api.github.com/repos/lodash/lodash/contents";
    private static readonly string GitHubToken = GetGitHubToken();

    static async Task Main(string[] args)
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        try
        {
            var filePaths = await GetAllFilePaths();
            var letterFrequency = new Dictionary<char, int>();

            foreach (var filePath in filePaths)
            {
                var content = await GetFileContent(filePath);
                CountLetters(content, letterFrequency);
            }

            var sortedFrequency = letterFrequency
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key);

            Console.WriteLine("Letter Frequency:");
            foreach (var kvp in sortedFrequency)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private static async Task<List<string>> GetAllFilePaths(string currentUrl = null)
    {
        using var httpClient = CreateHttpClient();
        var url = currentUrl ?? GitHubApiUrl;
        var filePaths = new List<string>();
        var nextUrl = url;

        do
        {
            var response = await httpClient.GetAsync(nextUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch data: {response.StatusCode}");
            }

            var items = JArray.Parse(await response.Content.ReadAsStringAsync());

            foreach (var item in items)
            {
                var path = item["path"]?.ToString();
                var type = item["type"]?.ToString();

                if (type == "file" && (path.EndsWith(".js") || path.EndsWith(".ts")))
                {
                    filePaths.Add(path);
                }
                else if (type == "dir")
                {
                    var subDirFiles = await GetAllFilePaths(item["url"]?.ToString());
                    filePaths.AddRange(subDirFiles);
                }
            }

            nextUrl = response.Headers.Contains("Link") ? GetNextPageLink(response.Headers.GetValues("Link").FirstOrDefault()) : null;
        } while (!string.IsNullOrEmpty(nextUrl));

        return filePaths;
    }

    private static async Task<string> GetFileContent(string filePath)
    {
        var fileUrl = $"https://raw.githubusercontent.com/lodash/lodash/master/{filePath}";
        using var httpClient = CreateHttpClient();
        return await httpClient.GetStringAsync(fileUrl);
    }

    private static void CountLetters(string content, Dictionary<char, int> frequency)
    {
        foreach (var character in content)
        {
            if (char.IsLetter(character))
            {
                var lowerChar = char.ToLower(character);
                if (frequency.ContainsKey(lowerChar))
                {
                    frequency[lowerChar]++;
                }
                else
                {
                    frequency[lowerChar] = 1;
                }
            }
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "DotNetApp");
        if (!string.IsNullOrEmpty(GitHubToken))
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", $"token {GitHubToken}");
        }
        return httpClient;
    }

    private static string GetNextPageLink(string linkHeader)
    {
        if (string.IsNullOrEmpty(linkHeader)) return null;

        var links = linkHeader.Split(',');
        foreach (var link in links)
        {
            var match = Regex.Match(link, "<(.*?)>; rel=\"next\"");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return null;
    }

    private static string GetGitHubToken()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();
        return config["GitHubToken"];
    }
}
