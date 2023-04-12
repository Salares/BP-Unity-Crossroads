using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelData 
{
    int[,] data = new int[,] { {0,1,1},{1,1,1},{1,1,0} };

    public int Width 
    {
        get { return data.GetLength(0); }
    }

    public int Depth 
    {
        get { return data.GetLength(1); }
    }

    public int GetCell(int x, int z)
    {
        return data[x,z];
    }

    public int GetNeighbor(int x, int z, Direction direction)
    {
        DataCoordinate offsetToCheck = offsets[(int)direction];
        DataCoordinate neighborCoordinate = new DataCoordinate(x+offsetToCheck.x, 0+offsetToCheck.y, z+offsetToCheck.z);

        if(neighborCoordinate.x < 0 || neighborCoordinate.x >= Width || neighborCoordinate.y != 0 || neighborCoordinate.z < 0 || neighborCoordinate.z >= Depth) 
        { 
            return 0; 
        } else 
        {
            return GetCell(neighborCoordinate.x,neighborCoordinate.z);
        }
    }

    struct DataCoordinate
    {
        public int x;
        public int y;
        public int z;

        public DataCoordinate(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    DataCoordinate[] offsets =
    {
        new DataCoordinate( 0, 0, 1 ),
        new DataCoordinate( 1, 0, 0 ),
        new DataCoordinate( 0, 0, -1),
        new DataCoordinate(-1, 0, 0 ),
        new DataCoordinate( 0, 1, 0 ),
        new DataCoordinate( 0,-1, 0 )    
    };

}

public enum Direction 
{
    North,
    East,
    South,
    West,
    Up,
    Down
}
