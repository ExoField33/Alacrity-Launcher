namespace Alacrity.Launcher.Core;

public readonly struct TerrariaVersionNumber : IComparable<TerrariaVersionNumber>
{
    private readonly int first;
    private readonly int second;
    private readonly int third;
    private readonly int fourth;
    private readonly int fifth;

    private TerrariaVersionNumber(int first, int second, int third, int fourth, int fifth, int componentCount)
    {
        this.first = first;
        this.second = second;
        this.third = third;
        this.fourth = fourth;
        this.fifth = fifth;
        ComponentCount = componentCount;
    }

    public int ComponentCount { get; }

    public static bool TryParse(string? value, out TerrariaVersionNumber version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        ReadOnlySpan<char> remaining = value.AsSpan();
        Span<int> components = stackalloc int[5];
        int componentCount = 0;

        while (!remaining.IsEmpty) {
            if (componentCount == components.Length) {
                return false;
            }

            int separatorIndex = remaining.IndexOf('.');
            ReadOnlySpan<char> component = separatorIndex >= 0 ? remaining.Slice(0, separatorIndex) : remaining;
            if (component.IsEmpty || !int.TryParse(component, out components[componentCount]) || components[componentCount] < 0) {
                return false;
            }

            componentCount++;
            if (separatorIndex < 0) {
                break;
            }

            remaining = remaining.Slice(separatorIndex + 1);
        }

        if (componentCount < 2) {
            return false;
        }

        version = new TerrariaVersionNumber(
            components[0],
            components[1],
            componentCount > 2 ? components[2] : 0,
            componentCount > 3 ? components[3] : 0,
            componentCount > 4 ? components[4] : 0,
            componentCount);
        return true;
    }

    public int CompareTo(TerrariaVersionNumber other)
    {
        int comparison = first.CompareTo(other.first);
        if (comparison != 0) {
            return comparison;
        }

        comparison = second.CompareTo(other.second);
        if (comparison != 0) {
            return comparison;
        }

        comparison = third.CompareTo(other.third);
        if (comparison != 0) {
            return comparison;
        }

        comparison = fourth.CompareTo(other.fourth);
        return comparison != 0 ? comparison : fifth.CompareTo(other.fifth);
    }

    public override string ToString()
    {
        return ComponentCount switch {
            2 => first + "." + second,
            3 => first + "." + second + "." + third,
            4 => first + "." + second + "." + third + "." + fourth,
            5 => first + "." + second + "." + third + "." + fourth + "." + fifth,
            _ => string.Empty
        };
    }
}
