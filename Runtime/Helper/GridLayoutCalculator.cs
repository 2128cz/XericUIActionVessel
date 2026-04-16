using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XericUI.Helper
{
    public static class GridLayoutCalculator
    {
        public struct LayoutItem
        {
            public Vector2 position; // anchoredPosition
            public Vector2 size;
            public int indexInGrid;
            public int rowIndex;
            public int columnIndex;
        }

        /// <summary>
        /// 计算所有项目的布局
        /// </summary>
        /// <param name="childCount">子项数量</param>
        /// <param name="containerSize">所需尺寸</param>
        /// <param name="cellSize">单元尺寸</param>
        /// <param name="spacing">单元空隙</param>
        /// <param name="padding">外围空隙</param>
        /// <param name="startCorner">起始角</param>
        /// <param name="startAxis">起始轴</param>
        /// <param name="constraint">容器尺寸</param>
        /// <param name="constraintCount">需要的列数</param>
        /// <param name="childAlignment">子项对齐</param>
        /// <returns></returns>
        public static List<LayoutItem> CalculateGridLayout(
            int childCount,
            Vector2 containerSize,
            Vector2 cellSize,
            Vector2 spacing,
            RectOffset padding,
            GridLayoutGroup.Corner startCorner,
            GridLayoutGroup.Axis startAxis,
            GridLayoutGroup.Constraint constraint,
            int constraintCount,
            TextAnchor childAlignment = TextAnchor.UpperLeft)
        {
            List<LayoutItem> layoutItems = new List<LayoutItem>(childCount);

            // 1. 计算行列数
            int cellCountX, cellCountY;
            CalculateGridDimensions(childCount, containerSize, cellSize, spacing, padding,
                constraint, constraintCount, startAxis, out cellCountX, out cellCountY);

            // 2. 计算实际使用的行列数
            int actualCellCountX, actualCellCountY;
            CalculateActualGridDimensions(childCount, cellCountX, cellCountY, constraint, constraintCount,
                startAxis, out actualCellCountX, out actualCellCountY);

            // 3. 计算所需空间和起始偏移
            Vector2 requiredSpace = new Vector2(
                actualCellCountX * cellSize.x + (actualCellCountX - 1) * spacing.x,
                actualCellCountY * cellSize.y + (actualCellCountY - 1) * spacing.y
            );

            Vector2 startOffset = new Vector2(
                GetStartOffset(0, requiredSpace.x, containerSize.x, padding, childAlignment),
                GetStartOffset(1, requiredSpace.y, containerSize.y, padding, childAlignment)
            );

            // 4. 处理特殊情况（约束条件下的子项重新排列）
            int childrenToMove = 0;
            if (childCount > constraintCount && constraint != GridLayoutGroup.Constraint.Flexible)
            {
                int cellsPerMainAxis = startAxis == GridLayoutGroup.Axis.Horizontal ? cellCountX : cellCountY;
                if (Mathf.CeilToInt((float)childCount / (float)cellsPerMainAxis) < constraintCount)
                {
                    childrenToMove = constraintCount - Mathf.CeilToInt((float)childCount / (float)cellsPerMainAxis);
                    childrenToMove += Mathf.FloorToInt((float)childrenToMove / ((float)cellsPerMainAxis - 1));
                    if (childCount % cellsPerMainAxis == 1)
                        childrenToMove += 1;
                }
            }

            // 5. 计算每个子项的位置
            int cornerX = (int)startCorner % 2;
            int cornerY = (int)startCorner / 2;

            for (int i = 0; i < childCount; i++)
            {
                int positionX, positionY;
                CalculateItemPosition(i, childCount, cellCountX, cellCountY, startAxis, constraint,
                    constraintCount, childrenToMove, cornerX, cornerY,
                    actualCellCountX, actualCellCountY, out positionX, out positionY);

                Vector2 position = new Vector2(
                    startOffset.x + (cellSize.x + spacing.x) * positionX,
                    startOffset.y + (cellSize.y + spacing.y) * positionY
                );

                layoutItems.Add(new LayoutItem
                {
                    position = position,
                    size = cellSize,
                    indexInGrid = i,
                    rowIndex = positionY,
                    columnIndex = positionX
                });
            }

            return layoutItems;
        }

        private static void CalculateGridDimensions(
            int childCount,
            Vector2 containerSize,
            Vector2 cellSize,
            Vector2 spacing,
            RectOffset padding,
            GridLayoutGroup.Constraint constraint,
            int constraintCount,
            GridLayoutGroup.Axis startAxis,
            out int cellCountX,
            out int cellCountY)
        {
            cellCountX = 1;
            cellCountY = 1;

            if (constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                cellCountX = constraintCount;
                if (childCount > cellCountX)
                    cellCountY = childCount / cellCountX + (childCount % cellCountX > 0 ? 1 : 0);
            }
            else if (constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                cellCountY = constraintCount;
                if (childCount > cellCountY)
                    cellCountX = childCount / cellCountY + (childCount % cellCountY > 0 ? 1 : 0);
            }
            else
            {
                // Flexible constraint - based on container size
                float availableWidth = containerSize.x - padding.horizontal;
                float availableHeight = containerSize.y - padding.vertical;

                if (cellSize.x + spacing.x > 0)
                    cellCountX = Mathf.Max(1,
                        Mathf.FloorToInt((availableWidth + spacing.x + 0.001f) / (cellSize.x + spacing.x)));
                else
                    cellCountX = int.MaxValue;

                if (cellSize.y + spacing.y > 0)
                    cellCountY = Mathf.Max(1,
                        Mathf.FloorToInt((availableHeight + spacing.y + 0.001f) / (cellSize.y + spacing.y)));
                else
                    cellCountY = int.MaxValue;
            }
        }

        private static void CalculateActualGridDimensions(
            int childCount,
            int cellCountX,
            int cellCountY,
            GridLayoutGroup.Constraint constraint,
            int constraintCount,
            GridLayoutGroup.Axis startAxis,
            out int actualCellCountX,
            out int actualCellCountY)
        {
            if (startAxis == GridLayoutGroup.Axis.Horizontal)
            {
                actualCellCountX = Mathf.Clamp(cellCountX, 1, childCount);

                if (constraint == GridLayoutGroup.Constraint.FixedRowCount)
                    actualCellCountY = Mathf.Min(cellCountY, childCount);
                else
                    actualCellCountY = Mathf.Clamp(cellCountY, 1, Mathf.CeilToInt(childCount / (float)cellCountX));
            }
            else
            {
                actualCellCountY = Mathf.Clamp(cellCountY, 1, childCount);

                if (constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                    actualCellCountX = Mathf.Min(cellCountX, childCount);
                else
                    actualCellCountX = Mathf.Clamp(cellCountX, 1, Mathf.CeilToInt(childCount / (float)cellCountY));
            }
        }

        private static void CalculateItemPosition(
            int index,
            int childCount,
            int cellCountX,
            int cellCountY,
            GridLayoutGroup.Axis startAxis,
            GridLayoutGroup.Constraint constraint,
            int constraintCount,
            int childrenToMove,
            int cornerX,
            int cornerY,
            int actualCellCountX,
            int actualCellCountY,
            out int positionX,
            out int positionY)
        {
            if (startAxis == GridLayoutGroup.Axis.Horizontal)
            {
                if (constraint == GridLayoutGroup.Constraint.FixedRowCount && childCount - index <= childrenToMove)
                {
                    positionX = 0;
                    positionY = constraintCount - (childCount - index);
                }
                else
                {
                    positionX = index % cellCountX;
                    positionY = index / cellCountX;
                }
            }
            else
            {
                if (constraint == GridLayoutGroup.Constraint.FixedColumnCount && childCount - index <= childrenToMove)
                {
                    positionX = constraintCount - (childCount - index);
                    positionY = 0;
                }
                else
                {
                    positionX = index / cellCountY;
                    positionY = index % cellCountY;
                }
            }

            // Apply corner flipping
            if (cornerX == 1)
                positionX = actualCellCountX - 1 - positionX;
            if (cornerY == 1)
                positionY = actualCellCountY - 1 - positionY;
        }

        private static float GetStartOffset(int axis, float requiredSpace, float availableSpace,
            RectOffset padding, TextAnchor childAlignment)
        {
            float requiredSpaceWithPadding = requiredSpace + (axis == 0 ? padding.horizontal : padding.vertical);
            float surplusSpace = availableSpace - requiredSpaceWithPadding;
            float alignmentOnAxis = GetAlignmentOnAxis(axis, childAlignment);

            return (axis == 0 ? padding.left : padding.top) + surplusSpace * alignmentOnAxis;
        }

        private static float GetAlignmentOnAxis(int axis, TextAnchor childAlignment)
        {
            if (axis == 0)
                return ((int)childAlignment % 3) * 0.5f;
            else
                return ((int)childAlignment / 3) * 0.5f;
        }
    }
}