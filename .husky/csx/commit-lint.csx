/// <summary>
/// a simple regex commit linter example
/// https://www.conventionalcommits.org/en/v1.0.0/
/// https://github.com/angular/angular/blob/22b96b9/CONTRIBUTING.md#type
/// </summary>

const string pattern = @"^(?=.{1,90}$)(?:build|feat|ci|chore|docs|fix|perf|refactor|revert|style|test)(?:\(.+\))*(?::).{4,}(?:#\d+)*(?<![\.\s])$";

var msg = File.ReadAllLines(Args[0])[0];
Console.WriteLine("Your commit headline message is:\n> {0}", msg);
if (System.Text.RegularExpressions.Regex.IsMatch(msg, pattern))
   return 0;

Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("Invalid commit message");
Console.ResetColor();
Console.WriteLine("e.g: 'feat(scope): subject' or 'fix: subject'");
Console.ForegroundColor = ConsoleColor.Gray;
Console.WriteLine("more info: https://www.conventionalcommits.org/en/v1.0.0/");

return 1;
