namespace D4LootBench.Core.Import;

public interface IBuildGuideParser
{
    ParsedBuildGuide Parse(string text);
}
