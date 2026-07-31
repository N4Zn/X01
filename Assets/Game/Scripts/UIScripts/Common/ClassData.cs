using System.Collections.Generic;

/// <summary>
/// A class (classroom) containing a list of students.
/// Persisted via DataManager.
/// </summary>
[System.Serializable]
public class ClassData
{
    public string ClassName;
    public List<PlayerInfo> Students;

    public ClassData()
    {
        ClassName = "";
        Students = new List<PlayerInfo>();
    }

    public ClassData(string className)
    {
        ClassName = className;
        Students = new List<PlayerInfo>();
    }
}
