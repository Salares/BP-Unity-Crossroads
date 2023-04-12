using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public class Crossroad : IEnumerable<Path>
{
    [SerializeField]
    private List<Path> pathList;

    public Crossroad(Vector2 centre, int numberOfPaths)
    {
        pathList = new List<Path>();
        for (int i = 0; i < numberOfPaths; i++)
        {
            pathList.Add(new Path(centre, i, numberOfPaths));
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
}
