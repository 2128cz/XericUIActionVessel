using System;
using System.Collections.Generic;

namespace XericUI.XChart.Editor
{
    /// <summary>
    /// 编辑器侧的测试数据生成器：<b>面板与数据窗口共用同一套口径</b>
    /// （原来"生成 32 条示例"写死在面板里，数据窗口要同一份形状就得抄一遍——抄一遍就会分叉）。
    ///
    /// <para>一律走 <c>TryAddRecord</c>：时间戳按上一条 +1（记录表要求时间非递减），
    /// 值个数必须等于数值维数量（否则模型直接拒收），所以先补齐/截断再提交。</para>
    /// </summary>
    public static class XChartDataGenerators
    {
        /// <summary>螺旋矢量场一条记录占的数值维：坐标 3 + 颜色 4（色相/饱和/明度/不透明度）</summary>
        public const int VectorFieldDimensions = 7;

        /// <summary>螺旋上参数 t∈[0,1] 的那个点（一圈正弦/余弦配线性 x）</summary>
        private static void Spiral(float t, out float x, out float y, out float z)
        {
            x = Lerp(-100f, 100f, t);
            y = (float)Math.Sin(t * Math.PI * 2.0) * 50f;
            z = (float)Math.Cos(t * Math.PI * 2.0) * 50f;
        }

        /// <summary>螺旋：一圈正弦/余弦配线性 x——现有"生成 32 条示例"就是它。</summary>
        public static int AppendSpiral(XChartDataModel model, int count)
        {
            if (model == null || count <= 0) return 0;

            int accepted = 0;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                float x, y, z;
                Spiral(t, out x, out y, out z);
                if (!TryAppend3(model, x, y, z)) break;
                accepted++;
            }
            return accepted;
        }

        /// <summary>
        /// 螺旋矢量场：一圈正弦/余弦点，**每条记录 7 个数值维** <c>[x, y, z, h, s, v, a]</c>——
        /// 前三维是<b>这条记录的坐标</b>（与圆柱/散点同一套口径），后四维是颜色
        /// （色相 0→256 走满一圈色环、饱和/明度 1、不透明度 1）。
        ///
        /// <para>箭头由**相邻两条记录**构成（规范 §25.2），所以两种读法在这份数据上一眼可辨（他 2026-10-25）：
        /// <b>相对</b>：箭头由本点指向下一点 ⇒ 一串首尾相接、顺着螺旋转；
        /// <b>绝对</b>：下一点的坐标当方向矢量 ⇒ 每根朝"下一点的坐标方向"散开，看起来像一片刺猬。
        /// 又因为颜色随记录自带，一串箭头的起点色与终点色**各自不同**。</para>
        ///
        /// <para>维数不够会先<b>把列补出来</b>（测试数据的判据是"点一下就能看到东西"，
        /// 而不是"提示他先去加四列"）；表里原有的记录会被清掉——它就是一份示例数据。</para>
        /// </summary>
        public static int AppendSpiralVectorField(XChartDataModel model, int count)
        {
            if (model == null || count <= 0) return 0;

            EnsureNumericDimensions(model, VectorFieldDimensions);
            if (model.NumericDimensionCount < VectorFieldDimensions) return 0;

            model.Clear();
            int accepted = 0;
            var values = new float[VectorFieldDimensions];
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                Spiral(t, out values[0], out values[1], out values[2]);
                values[3] = 256f * t;   // 色相：0..256 正好走完一圈色环
                values[4] = 1f;         // 饱和度
                values[5] = 1f;         // 明度
                values[6] = 1f;         // 不透明度
                if (!TryAppend(model, values)) break;
                accepted++;
            }
            return accepted;
        }

        /// <summary>把数值维补到至少 <paramref name="wanted"/> 个（按"数值维个数"补，不按表行数）。</summary>
        private static void EnsureNumericDimensions(XChartDataModel model, int wanted)
        {
            if (model.NumericDimensionCount >= wanted) return;

            var dimensions = new List<XChartDimensionDefinition>(model.Dimensions);
            int numeric = model.NumericDimensionCount;
            for (int i = numeric; i < wanted; i++)
            {
                dimensions.Add(new XChartDimensionDefinition("v" + i, "V" + i, XChartDimensionKind.Number));
            }
            model.SetDimensions(dimensions);
        }

        /// <summary>
        /// 平方和金字塔：第 <c>k</c> 层是 <c>k×k</c> 个顶点（1、4、9、16 ⇒ 30 个），
        /// 层内 x/y 铺满 <c>[-(k-1)/2, +(k-1)/2]</c>，<b>z = k 逐层升高</b>
        /// ⇒ 层数越高、点越多、位置越高的阶梯金字塔。
        /// </summary>
        public static int AppendSquarePyramid(XChartDataModel model, int layers)
        {
            if (model == null || layers <= 0) return 0;

            int accepted = 0;
            for (int k = 1; k <= layers; k++)
            {
                float half = (k - 1) * 0.5f;
                for (int i = 0; i < k; i++)
                {
                    for (int j = 0; j < k; j++)
                    {
                        float x = i - half;
                        float y = j - half;
                        if (!TryAppend3(model, x, y, k)) return accepted;
                        accepted++;
                    }
                }
            }
            return accepted;
        }

        /// <summary>追加一条：前三个数值维按顺序喂 x / y / z（其余补 0）。</summary>
        public static bool TryAppend3(XChartDataModel model, float x, float y, float z)
        {
            if (model == null) return false;

            int dimensions = model.NumericDimensionCount;
            if (dimensions <= 0) return false;

            var values = new float[dimensions];
            if (dimensions > 0) values[0] = x;
            if (dimensions > 1) values[1] = y;
            if (dimensions > 2) values[2] = z;
            return TryAppend(model, values);
        }

        /// <summary>追加一条：时间戳 = 上一条 +1（空表从 0 开始），其余交给模型校验。</summary>
        public static bool TryAppend(XChartDataModel model, float[] values)
        {
            if (model == null || values == null) return false;

            double timestamp = model.RecordCount > 0
                ? model.GetTimeAt(model.RecordCount - 1) + 1.0
                : 0.0;
            return model.TryAddRecord(timestamp, values);
        }

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;
    }
}