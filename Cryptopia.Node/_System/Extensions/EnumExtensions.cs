/// <summary>
/// https://stackoverflow.com/questions/16100/convert-a-string-to-an-enum-in-c-sharp
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Convert string into enum
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToEnum<T>(this string value)
    {
        return (T)Enum.Parse(typeof(T), value, true);
    }

    /// <summary>
    /// Try to convert string into enum
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryParseEnum<T>(this string value, out T? result)
    {
        bool success = Enum.TryParse(
            typeof(T), value, out object obj);

        if (success)
        {
            result = (T)obj;
        }
        else 
        {
            result = default;
        }
            
        return success;
    }

    /// <summary>
    /// Convert byte into enum
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public static T ToEnum<T>(this byte value)
    {
        return (T)Enum.ToObject(typeof(T), value);
    }

    /// <summary>
    /// Get enum values as a list of T
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<T> GetValues<T>()
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }
}
