using System.Text.RegularExpressions;


public class SourcePreprocessor{
    private static readonly Regex MatchPattern = new ("""(?<name>\w+)\s*=\s*"(?<value>[^"\r\n]*)" """.TrimEnd(),
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200)
    );
    
    public IEnumerable<ExtractedLiteral> Extract(SourceFile file)
    {   
        foreach(Match match in MatchPattern.Matches(file.Content))
        {
            string value = match.Groups["value"].Value;
            string variableName = match.Groups["name"].Value;
            
            int lineNumber = 1;

            for (int i = 0; i < match.Index; i++)
            {
                lineNumber ++;
            }

            yield return new ExtractedLiteral(
                value,
                variableName,
                file.Path,
                lineNumber
            );
        }
    }
}
