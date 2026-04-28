﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XericUI.Helper
{
    public static class GridLayoutCalculator
    {
        public static Vector2 Grid;

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
            ref List<LayoutItem> layoutItems,
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
            if (layoutItems == null)
                layoutItems = new List<LayoutItem>();
            else
                layoutItems.Clear();

            // 边界保护
            constraintCount = Mathf.Max(1, constraintCount);
            cellSize = new Vector2(Mathf.Max(0.1f, cellSize.x), Mathf.Max(0.1f, cellSize.y));
            spacing = new Vector2(Mathf.Max(0f, spacing.x), Mathf.Max(0f, spacing.y));

            // todo 这里的行列数限制了实际行列数，需要修改计算扩容
            // 1. 计算行列数
            int cellCountX, cellCountY;
            CalculateGridDimensions(childCount, containerSize, cellSize, spacing, padding,
                constraint, constraintCount, startAxis, out cellCountX, out cellCountY);

            // 2. 计算实际使用的行列数
            int actualCellCountX, actualCellCountY;
            CalculateActualGridDimensions(childCount, cellCountX, cellCountY, constraint, constraintCount,
                startAxis, out actualCellCountX, out actualCellCountY);

            Grid = new Vector2(actualCellCountX, actualCellCountY);

            // 3. 计算所需空间
            Vector2 requiredSpace = new Vector2(
                actualCellCountX * cellSize.x + Mathf.Max(0, actualCellCountX - 1) * spacing.x,
                actualCellCountY * cellSize.y + Mathf.Max(0, actualCellCountY - 1) * spacing.y
            );

            // 4. 计算起始偏移（关键修复：根据startCorner调整基准点）
            Vector2 startOffset = CalculateStartOffset(
                requiredSpace, containerSize, padding, childAlignment, startCorner);

            // 5. 处理特殊情况

            int childrenToMove = CalculateChildrenToMove(childCount, cellCountX, cellCountY,
                constraint, constraintCount, startAxis);

            // 6. 计算每个子项的位置
            int cornerX = (int)startCorner % 2; // 0=左, 1=右
            int cornerY = (int)startCorner / 2; // 0=上, 1=下

            for (int i = 0; i < childCount; i++)
            {
                int gridX, gridY;
                CalculateItemGridPosition(i, childCount, cellCountX, cellCountY, startAxis,
                    constraint, constraintCount, childrenToMove,
                    actualCellCountX, actualCellCountY, out gridX, out gridY);

                // 应用corner翻转（关键：这决定了索引0的位置）
                if (cornerX == 1)
                    gridX = actualCellCountX - 1 - gridX;
                if (cornerY == 1)
                    gridY = actualCellCountY - 1 - gridY;

                // 计算最终位置（固定方向：向右下）
                Vector2 position = new Vector2(
                    startOffset.x + (cellSize.x + spacing.x) * gridX,
                    startOffset.y + (cellSize.y + spacing.y) * gridY
                );

                layoutItems.Add(new LayoutItem
                {
                    position = position,
                    size = cellSize,
                    indexInGrid = i,
                    rowIndex = gridY,
                    columnIndex = gridX
                });
            }

            return layoutItems;
        }

        // ===== 核心修复：根据startCorner计算起始偏移 =====
        private static Vector2 CalculateStartOffset(
            Vector2 requiredSpace,
            Vector2 containerSize,
            RectOffset padding,
            TextAnchor childAlignment,
            GridLayoutGroup.Corner startCorner)
        {
            // 计算每个轴的起始偏移
            float startX = CalculateAxisStartOffset(
                0, requiredSpace.x, containerSize.x, padding, childAlignment, startCorner);
            float startY = CalculateAxisStartOffset(
                1, requiredSpace.y, containerSize.y, padding, childAlignment, startCorner);

            return new Vector2(startX, startY);
        }

        private static float CalculateAxisStartOffset(
            int axis, // 0=X, 1=Y
            float requiredSpace,
            float availableSpace,
            RectOffset padding,
            TextAnchor childAlignment,
            GridLayoutGroup.Corner startCorner)
        {
            // 计算包含padding的总需求空间
            float requiredSpaceWithPadding = requiredSpace + (axis == 0 ? padding.horizontal : padding.vertical);

            // 计算剩余空间
            float surplusSpace = availableSpace - requiredSpaceWithPadding;

            // 获取对齐值（0=左/上, 0.5=中, 1=右/下）
            float alignmentValue = GetAlignmentOnAxis(axis, childAlignment);

            // 关键：根据startCorner调整基准点
            float baseOffset;
            if (axis == 0) // X轴
            {
                // 如果startCorner在右侧，基准点应该是availableSpace - padding.right - requiredSpace
                int cornerX = (int)startCorner % 2;
                baseOffset = (cornerX == 0) ? padding.left : (availableSpace - padding.right - requiredSpace);
            }
            else // Y轴
            {
                // 如果startCorner在底部，基准点应该是availableSpace - padding.bottom - requiredSpace
                int cornerY = (int)startCorner / 2;
                baseOffset = (cornerY == 0) ? padding.top : (availableSpace - padding.bottom - requiredSpace);
            }

            // 应用对齐（在剩余空间内）
            return baseOffset + surplusSpace * alignmentValue;
        }

        private static void CalculateItemGridPosition(
            int index,
            int childCount,
            int cellCountX,
            int cellCountY,
            GridLayoutGroup.Axis startAxis,
            GridLayoutGroup.Constraint constraint,
            int constraintCount,
            int childrenToMove,
            int actualCellCountX,
            int actualCellCountY,
            out int gridX,
            out int gridY)
        {
            // 计算基础位置（不考虑corner翻转）
            if (startAxis == GridLayoutGroup.Axis.Horizontal)
            {
                if (constraint == GridLayoutGroup.Constraint.FixedRowCount &&
                    childrenToMove > 0 && childCount - index <= childrenToMove)
                {
                    // 计算偏移量，确保gridY不会为负数
                    int offset = childCount - index - 1;
                    gridX = 0;
                    gridY = Mathf.Min(offset, constraintCount - 1);
                }
                else
                {
                    gridX = index % cellCountX;
                    gridY = index / cellCountX;
                }
            }
            else
            {
                if (constraint == GridLayoutGroup.Constraint.FixedColumnCount &&
                    childrenToMove > 0 && childCount - index <= childrenToMove)
                {
                    // 计算偏移量，确保gridX不会为负数
                    int offset = childCount - index - 1;
                    gridX = Mathf.Min(offset, constraintCount - 1);
                    gridY = 0;
                }
                else
                {
                    gridX = index / cellCountY;
                    gridY = index % cellCountY;
                }
            }
        }

        private static int CalculateChildrenToMove(
            int childCount,
            int cellCountX,
            int cellCountY,
            GridLayoutGroup.Constraint constraint,
            int constraintCount,
            GridLayoutGroup.Axis startAxis)
        {
            if (constraint == GridLayoutGroup.Constraint.Flexible || childCount <= constraintCount)
                return 0;

            int cellsPerMainAxis = startAxis == GridLayoutGroup.Axis.Horizontal ? cellCountX : cellCountY;

            if (cellsPerMainAxis <= 0)
                return 0;

            float rowsNeeded = Mathf.CeilToInt((float)childCount / (float)cellsPerMainAxis);
            if (rowsNeeded >= constraintCount)
                return 0;

            int childrenToMove = constraintCount - (int)rowsNeeded;
            childrenToMove += Mathf.FloorToInt((float)childrenToMove / Mathf.Max(1f, (float)cellsPerMainAxis - 1));

            if (childCount % cellsPerMainAxis == 1)
                childrenToMove += 1;

            return Mathf.Clamp(childrenToMove, 0, childCount);
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

            switch (constraint)
            {
                case GridLayoutGroup.Constraint.FixedColumnCount:
                    cellCountX = constraintCount;
                    if (childCount > cellCountX)
                        cellCountY = childCount / cellCountX + (childCount % cellCountX > 0 ? 1 : 0);
                    break;
                case GridLayoutGroup.Constraint.FixedRowCount:
                    cellCountY = constraintCount;
                    if (childCount > cellCountY)
                        cellCountX = childCount / cellCountY + (childCount % cellCountY > 0 ? 1 : 0);
                    break;
                default:
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
                    break;
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

        private static float GetAlignmentOnAxis(int axis, TextAnchor childAlignment)
        {
            if (axis == 0)
                return ((int)childAlignment % 3) * (1f / 3f); // 0, 0.333, 0.666
            else
                return ((int)childAlignment / 3) * 0.5f; // 0, 0.5, 1
        }
    }
}