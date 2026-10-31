using System.Text.RegularExpressions;

// Source Preprocessor - 
// extracts regex-matched literals and returns a loaded record of ExtractedLiteral type

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
            
            // Calculate the captured match lineNumber
            int charIndex = match.Groups["value"].Index;
            int lineNumber = 1;
            for (int i = 0; i < charIndex; i++)
            {
                if (file.Content[i] == '\n')
                {
                    lineNumber ++;
                }
                
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
