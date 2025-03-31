using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Crossroad : IEnumerable<Path>
{
    private List<Path> pathList;

    public Crossroad(Vector3 centre, int numberOfPaths, float startPointOffset, float endPointOffset, float controlPointOffset)
    {
        pathList = new List<Path>();
        for (int i = 0; i < numberOfPaths; i++)
        {
            Path path = new Path(centre, i, numberOfPaths, startPointOffset, endPointOffset, controlPointOffset);
            pathList.Add(path);
        }
    }

    public Path this[int i]
    {
        get { return pathList[i]; }
    }

    public IEnumerator<Path> GetEnumerator()
    {
        return pathList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => pathList.Count;

}
