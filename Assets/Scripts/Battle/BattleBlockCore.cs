using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleBlockShapeId
{
    H3,
    L3,
    H4,
    O4,
    T4,
    L4,
    L4_M,
    Z4,
    Z4_M,
    H5,
    P5,
    T5,
    L5,
    CurseDiag3,
    CurseSplit3,
    CurseDiag4,
    CurseSplit4
}

[Serializable]
public sealed class BattleBlockSpriteSet
{
    public Sprite h3CellSprite;
    public Sprite l3CellSprite;
    public Sprite h4CellSprite;
    public Sprite o4CellSprite;
    public Sprite t4CellSprite;
    public Sprite l4CellSprite;
    public Sprite l4MCellSprite;
    public Sprite z4CellSprite;
    public Sprite z4MCellSprite;
    public Sprite h5CellSprite;
    public Sprite p5CellSprite;
    public Sprite t5CellSprite;
    public Sprite l5CellSprite;
    public Sprite curseDiag3CellSprite;
    public Sprite curseSplit3CellSprite;
    public Sprite curseDiag4CellSprite;
    public Sprite curseSplit4CellSprite;
}

public sealed class BattleBlockShape
{
    public BattleBlockShapeId shapeId;
    public int weight;
    public Color color;
    public List<Vector2Int> baseCells;
    public bool isCurse;
    public Sprite cellSprite;

    public BattleBlockShape(
        BattleBlockShapeId shapeId,
        int weight,
        Color color,
        List<Vector2Int> baseCells,
        bool isCurse,
        Sprite cellSprite)
    {
        this.shapeId = shapeId;
        this.weight = weight;
        this.color = color;
        this.baseCells = baseCells;
        this.isCurse = isCurse;
        this.cellSprite = cellSprite;
    }
}

public sealed class BattleBlockInstance
{
    public BattleBlockShapeId shapeId;
    public int rotation;
    public Color color;
    public List<Vector2Int> cells;
    public bool isCurse;
    public Sprite cellSprite;

    public int CellCount => cells != null ? cells.Count : 0;
}

public static class BattleBlockCore
{
    public const int BoardSize = 8;

    public static readonly Color32 BoardBaseColor = new Color32(255, 255, 255, 255);
    public static readonly Color32 BlockColor1 = new Color32(255, 255, 255, 255);//  7E6757F (230, 117, 127, 255) 
    public static readonly Color32 BlockColor2 = new Color32(255, 255, 255, 255); // B471E8 (180, 113, 232, 255)
    public static readonly Color32 BlockColor3 = new Color32(255, 255, 255, 255); // E6D56B (230, 213, 107, 255)
    public static readonly Color32 BlockColor4 = new Color32(255, 255, 255, 255); // 6BE482 (107, 228, 130, 255)
    public static readonly Color32 BlockColor5 = new Color32(255, 255, 255, 255);// 709AE7 (112, 154, 231, 255)
    public static readonly Color32 CurseColor = new Color32(170, 90, 220, 255);

    public static void BuildShapeLibrary(
        List<BattleBlockShape> normalShapeLibrary,
        List<BattleBlockShape> curseShapeLibrary,
        BattleBlockSpriteSet sprites)
    {
        normalShapeLibrary.Clear();
        curseShapeLibrary.Clear();

        AddShape(normalShapeLibrary, BattleBlockShapeId.H3, 90, BlockColor1, false, sprites.h3CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0));

        AddShape(normalShapeLibrary, BattleBlockShapeId.L3, 90, BlockColor2, false, sprites.l3CellSprite,
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(normalShapeLibrary, BattleBlockShapeId.H4, 90, BlockColor3, false, sprites.h4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0));

        AddShape(normalShapeLibrary, BattleBlockShapeId.O4, 60, BlockColor4, false, sprites.o4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(normalShapeLibrary, BattleBlockShapeId.T4, 85, BlockColor5, false, sprites.t4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1));

        AddShape(normalShapeLibrary, BattleBlockShapeId.L4, 85, BlockColor1, false, sprites.l4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2));

        AddShape(normalShapeLibrary, BattleBlockShapeId.L4_M, 85, BlockColor2, false, sprites.l4MCellSprite,
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(0, 2));

        AddShape(normalShapeLibrary, BattleBlockShapeId.Z4, 75, BlockColor3, false, sprites.z4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1));

        AddShape(normalShapeLibrary, BattleBlockShapeId.Z4_M, 75, BlockColor4, false, sprites.z4MCellSprite,
            new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(normalShapeLibrary, BattleBlockShapeId.H5, 55, BlockColor5, false, sprites.h5CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0));

        AddShape(normalShapeLibrary, BattleBlockShapeId.P5, 70, BlockColor1, false, sprites.p5CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, 2));

        AddShape(normalShapeLibrary, BattleBlockShapeId.T5, 70, BlockColor2, false, sprites.t5CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(1, 2));

        AddShape(normalShapeLibrary, BattleBlockShapeId.L5, 65, BlockColor3, false, sprites.l5CellSprite,
            new Vector2Int(0, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, 2),
            new Vector2Int(1, 0),
            new Vector2Int(2, 0));

        AddShape(curseShapeLibrary, BattleBlockShapeId.CurseDiag3, 100, CurseColor, true, sprites.curseDiag3CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2));

        AddShape(curseShapeLibrary, BattleBlockShapeId.CurseSplit3, 100, CurseColor, true, sprites.curseSplit3CellSprite,
            new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(1, 2));

        AddShape(curseShapeLibrary, BattleBlockShapeId.CurseDiag4, 100, CurseColor, true, sprites.curseDiag4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2), new Vector2Int(3, 3));

        AddShape(curseShapeLibrary, BattleBlockShapeId.CurseSplit4, 100, CurseColor, true, sprites.curseSplit4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(0, 2), new Vector2Int(2, 2));
    }

    public static BattleBlockInstance CreateRandomNormalBlock(List<BattleBlockShape> normalShapeLibrary)
    {
        BattleBlockShape picked = PickWeighted(normalShapeLibrary);
        int rotation = UnityEngine.Random.Range(0, 4);
        return CreateBlockInstance(picked, rotation);
    }

    public static BattleBlockInstance CreateBlockInstance(BattleBlockShape shape, int rotation)
    {
        List<Vector2Int> rotated = RotateAndNormalize(shape.baseCells, rotation);

        return new BattleBlockInstance
        {
            shapeId = shape.shapeId,
            rotation = rotation,
            color = shape.color,
            cells = rotated,
            isCurse = shape.isCurse,
            cellSprite = shape.cellSprite
        };
    }

    public static bool CanPlaceBlock(BattleBlockInstance block, bool[,] occupied, int anchorX, int anchorY)
    {
        if (block == null || block.cells == null)
            return false;

        for (int i = 0; i < block.cells.Count; i++)
        {
            int x = anchorX + block.cells[i].x;
            int y = anchorY + block.cells[i].y;

            if (x < 0 || x >= BoardSize || y < 0 || y >= BoardSize)
                return false;

            if (occupied[x, y])
                return false;
        }

        return true;
    }

    public static void PlaceBlock(
        BattleBlockInstance block,
        bool[,] occupied,
        Color[,] colors,
        Sprite[,] blockSprites,
        int anchorX,
        int anchorY)
    {
        if (block == null)
            return;

        for (int i = 0; i < block.cells.Count; i++)
        {
            int x = anchorX + block.cells[i].x;
            int y = anchorY + block.cells[i].y;

            occupied[x, y] = true;
            colors[x, y] = block.color;
            blockSprites[x, y] = block.cellSprite;
        }
    }

    public static int ClearCompletedLines(
        bool[,] occupied,
        Color[,] colors,
        Sprite[,] blockSprites)
    {
        bool[] clearRows = new bool[BoardSize];
        bool[] clearCols = new bool[BoardSize];

        int rowCount = 0;
        int colCount = 0;

        for (int y = 0; y < BoardSize; y++)
        {
            bool full = true;
            for (int x = 0; x < BoardSize; x++)
            {
                if (!occupied[x, y])
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                clearRows[y] = true;
                rowCount++;
            }
        }

        for (int x = 0; x < BoardSize; x++)
        {
            bool full = true;
            for (int y = 0; y < BoardSize; y++)
            {
                if (!occupied[x, y])
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                clearCols[x] = true;
                colCount++;
            }
        }

        if (rowCount == 0 && colCount == 0)
            return 0;

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (!clearRows[y] && !clearCols[x])
                    continue;

                occupied[x, y] = false;
                colors[x, y] = Color.clear;
                blockSprites[x, y] = null;
            }
        }

        return rowCount + colCount;
    }

    public static bool HasAnyPlaceableMove(IReadOnlyList<BattleBlockInstance> blocks, bool[,] occupied)
    {
        if (blocks == null)
            return false;

        for (int i = 0; i < blocks.Count; i++)
        {
            BattleBlockInstance block = blocks[i];
            if (block == null)
                continue;

            for (int y = 0; y < BoardSize; y++)
            {
                for (int x = 0; x < BoardSize; x++)
                {
                    if (CanPlaceBlock(block, occupied, x, y))
                        return true;
                }
            }
        }

        return false;
    }

    private static void AddShape(
        List<BattleBlockShape> target,
        BattleBlockShapeId shapeId,
        int weight,
        Color color,
        bool isCurse,
        Sprite cellSprite,
        params Vector2Int[] cells)
    {
        target.Add(new BattleBlockShape(
            shapeId,
            weight,
            color,
            new List<Vector2Int>(cells),
            isCurse,
            cellSprite));
    }

    private static BattleBlockShape PickWeighted(List<BattleBlockShape> shapes)
    {
        int totalWeight = 0;

        for (int i = 0; i < shapes.Count; i++)
            totalWeight += Mathf.Max(1, shapes[i].weight);

        int pick = UnityEngine.Random.Range(0, totalWeight);

        for (int i = 0; i < shapes.Count; i++)
        {
            int weight = Mathf.Max(1, shapes[i].weight);
            if (pick < weight)
                return shapes[i];

            pick -= weight;
        }

        return shapes[shapes.Count - 1];
    }

    private static List<Vector2Int> RotateAndNormalize(List<Vector2Int> cells, int rotation)
    {
        List<Vector2Int> result = new List<Vector2Int>(cells.Count);

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int c = cells[i];
            Vector2Int r;

            switch (rotation % 4)
            {
                case 0:
                    r = new Vector2Int(c.x, c.y);
                    break;
                case 1:
                    r = new Vector2Int(-c.y, c.x);
                    break;
                case 2:
                    r = new Vector2Int(-c.x, -c.y);
                    break;
                default:
                    r = new Vector2Int(c.y, -c.x);
                    break;
            }

            result.Add(r);
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;

        for (int i = 0; i < result.Count; i++)
        {
            if (result[i].x < minX) minX = result[i].x;
            if (result[i].y < minY) minY = result[i].y;
        }

        for (int i = 0; i < result.Count; i++)
            result[i] = new Vector2Int(result[i].x - minX, result[i].y - minY);

        return result;
    }
}