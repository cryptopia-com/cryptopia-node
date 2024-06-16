/// <summary>
/// Guarantees a single instance of a type
/// </summary>
/// <typeparam name="T">The type of the singleton class.</typeparam>
public class Singleton<T> where T : new()
{
    /// <summary>
    /// Access singleton instance
    /// </summary>
    public static T Instance
    {
        get
        {
            return _Instance;
        }
    }
    private static readonly T _Instance = new T();

    /// <summary>
    /// Private constructor to prevent instantiation
    /// </summary>
    protected Singleton() {}

    /// <summary>
    /// Static constructor to ensure thread-safe initialization
    /// </summary>
    static Singleton() { }
}
