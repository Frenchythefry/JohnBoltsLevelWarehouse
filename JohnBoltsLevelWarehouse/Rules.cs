using System.Collections.Generic;
using Newtonsoft.Json;

public class Rules
{
    public bool allowSwap;
    public int allowedAbility;
    public (float x, float y) startPos;
    public (int width, int height) dimensions;
    public string name;
    public List<(Case, List<float>)> other;
    public float[] times;
    public static readonly Dictionary<Case, int> numParams = new Dictionary<Case, int>
    {
        {Case.Win, 2},
        {Case.Checkpoint, 2}
    };
    public Rules(bool allowSwap, int allowedAbility, (float x, float y) startPos, (int width, int height) dimensions, string name, List<(Case, List<float>)> other)
    {
        this.allowSwap = allowSwap;
        this.allowedAbility = allowedAbility;
        this.startPos = startPos;
        this.dimensions = dimensions;
        this.name = name;
        this.other = other;
        this.times = new float[]
        {
            60f,
            30f,
            15f
        };
    }
    [JsonConstructor]
    public Rules(bool allowSwap, int allowedAbility, (float x, float y) startPos, (int width, int height) dimensions, string name, List<(Case, List<float>)> other, float[] times)
    {
        this.allowSwap = allowSwap;
        this.allowedAbility = allowedAbility;
        this.startPos = startPos;
        this.dimensions = dimensions;
        this.name = name;
        this.other = other;
        this.times = times;
    }

}
public enum Case
{
    Win,
    Checkpoint
}