using Octokit;
using System.Collections.Generic;

public class SecretDetector
{
    private readonly SourcePreprocessor _preprocessor;
    public SecretDetector(SourcePreprocessor preprocessor)
    {
        _preprocessor = preprocessor;
    }

    public IEnumerable<ExtractedLiteral> Detect(SourceFile file)
    {
        return _preprocessor.Extract(file);
    }
}