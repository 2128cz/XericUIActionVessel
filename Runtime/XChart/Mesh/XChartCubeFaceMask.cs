using System;

namespace XericUI.XChart.Mesh
{
    /// <summary>
    /// 立方体六面的选择掩码。用它可以精确控制"渲染哪些面"（填充与网格各自独立一份掩码）。
    ///
    /// <para><b>面编号契约（固定不变）</b></para>
    /// <code>
    ///   a = 前 Front   (法线 +Z)      d = 下 Down  (法线 -Y)
    ///   b = 后 Back    (法线 -Z)      e = 左 Left  (法线 -X)
    ///   c = 上 Up      (法线 +Y)      f = 右 Right (法线 +X)
    /// </code>
    /// 字母按<b>语义</b>绑定，与内部遍历顺序无关：<see cref="XChartCubeGeometry.CubeFace"/> 的枚举值
    /// 顺序将来即使调整，字母也不会跟着漂。
    ///
    /// <para><b>注意</b>：掩码是"画哪些面"的唯一依据，<b>不看可见性</b>——图表里的背景网格
    /// 恰恰常画在背朝观察者的那几面（后墙 / 底面）。要只画朝向观察者的面请自行用
    /// <see cref="XChartCubeGeometry.IsFaceVisible"/> 过滤。</para>
    /// </summary>
    [Flags]
    public enum XChartCubeFaceMask
    {
        None = 0,
        /// <summary>a 前</summary>
        Front = 1 << 0,
        /// <summary>b 后</summary>
        Back = 1 << 1,
        /// <summary>c 上</summary>
        Up = 1 << 2,
        /// <summary>d 下</summary>
        Down = 1 << 3,
        /// <summary>e 左</summary>
        Left = 1 << 4,
        /// <summary>f 右</summary>
        Right = 1 << 5,

        All = Front | Back | Up | Down | Left | Right,

        /// <summary>三面（后 + 下 + 左）：常见的"背景墙"一组</summary>
        BackWalls = Back | Down | Left,
    }

    /// <summary>面掩码与面之间的换算（与 <see cref="XChartCubeFaceMask"/> 的字母契约配套）</summary>
    public static class XChartCubeFaceMaskUtility
    {
        /// <summary>面的稳定字母（a=前 b=后 c=上 d=下 e=左 f=右）</summary>
        public static char GetLetter(XChartCubeGeometry.CubeFace face)
        {
            switch (face)
            {
                case XChartCubeGeometry.CubeFace.Front: return 'a';
                case XChartCubeGeometry.CubeFace.Back: return 'b';
                case XChartCubeGeometry.CubeFace.Top: return 'c';
                case XChartCubeGeometry.CubeFace.Bottom: return 'd';
                case XChartCubeGeometry.CubeFace.Left: return 'e';
                default: return 'f';
            }
        }

        /// <summary>字母反查面</summary>
        public static bool TryParseLetter(char letter, out XChartCubeGeometry.CubeFace face)
        {
            switch (char.ToLowerInvariant(letter))
            {
                case 'a': face = XChartCubeGeometry.CubeFace.Front; return true;
                case 'b': face = XChartCubeGeometry.CubeFace.Back; return true;
                case 'c': face = XChartCubeGeometry.CubeFace.Top; return true;
                case 'd': face = XChartCubeGeometry.CubeFace.Bottom; return true;
                case 'e': face = XChartCubeGeometry.CubeFace.Left; return true;
                case 'f': face = XChartCubeGeometry.CubeFace.Right; return true;
                default: face = XChartCubeGeometry.CubeFace.Front; return false;
            }
        }

        /// <summary>单个面对应的掩码位</summary>
        public static XChartCubeFaceMask GetMask(XChartCubeGeometry.CubeFace face)
        {
            switch (face)
            {
                case XChartCubeGeometry.CubeFace.Front: return XChartCubeFaceMask.Front;
                case XChartCubeGeometry.CubeFace.Back: return XChartCubeFaceMask.Back;
                case XChartCubeGeometry.CubeFace.Top: return XChartCubeFaceMask.Up;
                case XChartCubeGeometry.CubeFace.Bottom: return XChartCubeFaceMask.Down;
                case XChartCubeGeometry.CubeFace.Left: return XChartCubeFaceMask.Left;
                default: return XChartCubeFaceMask.Right;
            }
        }

        /// <summary>掩码里是否包含某个面</summary>
        public static bool HasFace(XChartCubeFaceMask mask, XChartCubeGeometry.CubeFace face)
            => (mask & GetMask(face)) != 0;

        /// <summary>
        /// 掩码的<b>字母写法</b>（<c>a=前 b=后 c=上 d=下 e=左 f=右</c>，按面号升序），空掩码写作 <c>-</c>。
        ///
        /// <para>报表与调试行要说"这一帧工具手上到底是哪几面"，直接印枚举的<b>数字</b>没用——
        /// 面板上的掩码下拉显示的是字母组合，数字对不上眼睛；而印枚举名（<c>Back | Down | Left</c>）
        /// 又会随枚举定义漂移。字母是这份契约里唯一钉住的语义（见类型注释），所以读数用它。</para>
        /// </summary>
        public static string Describe(XChartCubeFaceMask mask)
        {
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                var face = (XChartCubeGeometry.CubeFace)i;
                if (!HasFace(mask, face)) continue;
                builder.Append(GetLetter(face));
            }
            return builder.Length > 0 ? builder.ToString() : "-";
        }
    }
}