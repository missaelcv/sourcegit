using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SourceGit.Helpers {

    /// <summary>
    ///     Tools to parse commit graph.
    /// </summary>
    public class CommitGraphMaker {
        /// <summary>
        ///     Sizes
        /// </summary>
        public static readonly double UNIT_WIDTH = 12;
        public static readonly double HALF_WIDTH = 6;
        public static readonly double DOUBLE_WIDTH = 24;
        public static readonly double UNIT_HEIGHT = 24;
        public static readonly double HALF_HEIGHT = 12;

        /// <summary>
        ///     Colors
        /// </summary>
        public static Brush[] Colors = new Brush[] {
            Brushes.Orange,
            Brushes.ForestGreen,
            Brushes.Gold,
            Brushes.Magenta,
            Brushes.Red,
            Brushes.Gray,
            Brushes.Turquoise,
            Brushes.Olive,
        };

        /// <summary>
        ///     Helpers to draw lines.
        /// </summary>
        public class LineHelper {
            private double lastX = 0;
            private double lastY = 0;

            /// <summary>
            ///     Parent commit id.
            /// </summary>
            public string Next { get; set; }

            /// <summary>
            ///     Is merged into this tree.
            /// </summary>
            public bool IsMerged { get; set; }

            /// <summary>
            ///     Points in line
            /// </summary>
            public List<Point> Points { get; set; }

            /// <summary>
            ///     Brush to draw line
            /// </summary>
            public Brush Brush { get; set; }

            /// <summary>
            ///     Current horizontal offset.
            /// </summary>
            public double HorizontalOffset => lastX;

            /// <summary>
            ///     Constructor.
            /// </summary>
            /// <param name="nextCommitId">Parent commit id</param>
            /// <param name="isMerged">Is merged in tree</param>
            /// <param name="colorIdx">Color index</param>
            /// <param name="startPoint">Start point</param>
            public LineHelper(string nextCommitId, bool isMerged, int colorIdx, Point startPoint) {
                Next = nextCommitId;
                IsMerged = isMerged;
                Points = new List<Point>() { startPoint };
                Brush = Colors[colorIdx % Colors.Length];

                lastX = startPoint.X;
                lastY = startPoint.Y;
            }

            /// <summary>
            ///     Line to.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="isEnd"></param>
            public void AddPoint(double x, double y, bool isEnd = false) {
                if (x > lastX) {
                    Points.Add(new Point(lastX, lastY));
                    Points.Add(new Point(x, y - HALF_HEIGHT));
                } else if (x < lastX) {
                    Points.Add(new Point(lastX, lastY + HALF_HEIGHT));
                    Points.Add(new Point(x, y));
                }

                lastX = x;
                lastY = y;

                if (isEnd) {
                    var last = Points.Last();
                    if (last.X != lastX || last.Y != lastY) Points.Add(new Point(lastX, lastY));
                }
            }
        }

        /// <summary>
        ///     Short link between two commits.
        /// </summary>
        public struct ShortLink {
            public Point Start;
            public Point Control;
            public Point End;
            public Brush Brush;
        }

        /// <summary>
        ///     Dot
        /// </summary>
        public struct Dot {
            public double X;
            public double Y;
            public Brush Color;
        }

        /// <summary>
        ///     Independent lines in graph
        /// </summary>
        public List<LineHelper> Lines { get; set; } = new List<LineHelper>();

        /// <summary>
        ///     Short links.
        /// </summary>
        public List<ShortLink> Links { get; set; } = new List<ShortLink>();

        /// <summary>
        ///     All dots.
        /// </summary>
        public List<Dot> Dots { get; set; } = new List<Dot>();

        /// <summary>
        ///     Parse commits.
        /// </summary>
        /// <param name="commits"></param>
        /// <returns></returns>
        public static CommitGraphMaker Parse(List<Git.Commit> commits) {
            CommitGraphMaker maker = new CommitGraphMaker();

            List<LineHelper> unsolved = new List<LineHelper>();
            List<LineHelper> ended = new List<LineHelper>();
            Dictionary<string, LineHelper> currentMap = new Dictionary<string, LineHelper>();
            double offsetY = -HALF_HEIGHT;
            int colorIdx = 0;

            for (int i = 0; i < commits.Count; i++) {
                Git.Commit commit = commits[i];
                LineHelper major = null;
                bool isMerged = commit.IsHEAD || commit.IsMerged;
                int oldCount = unsolved.Count;

                // 更新Y坐标
                offsetY += UNIT_HEIGHT;

                // 找到第一个依赖于本提交的树，将其他依赖于本提交的树标记为终止，并对已存在的线路调整（防止线重合）
                double offsetX = -HALF_WIDTH;
                foreach (var l in unsolved) {
                    if (l.Next == commit.SHA) {
                        if (major == null) {
                            offsetX += UNIT_WIDTH;
                            major = l;

                            if (commit.Parents.Count > 0) {
                                major.Next = commit.Parents[0];
                                if (!currentMap.ContainsKey(major.Next)) currentMap.Add(major.Next, major);
                            } else {
                                major.Next = "ENDED";
                                ended.Add(l);
                            }

                            major.AddPoint(offsetX, offsetY);
                        } else {
                            ended.Add(l);
                        }

                        isMerged = isMerged || l.IsMerged;
                    } else {
                        if (!currentMap.ContainsKey(l.Next)) currentMap.Add(l.Next, l);
                        offsetX += UNIT_WIDTH;
                        l.AddPoint(offsetX, offsetY);
                    }
                }

                // 处理本提交为非当前分支HEAD的情况（创建新依赖线路）
                if (major == null && commit.Parents.Count > 0) {
                    offsetX += UNIT_WIDTH;
                    major = new LineHelper(commit.Parents[0], isMerged, colorIdx, new Point(offsetX, offsetY));
                    unsolved.Add(major);
                    colorIdx++;
                }

                // 确定本提交的点的位置
                Point position = new Point(offsetX, offsetY);
                if (major != null) {
                    major.IsMerged = isMerged;
                    position.X = major.HorizontalOffset;
                    position.Y = offsetY;
                    maker.Dots.Add(new Dot() { X = position.X - 3, Y = position.Y - 3, Color = major.Brush });
                } else {
                    maker.Dots.Add(new Dot() { X = position.X - 3, Y = position.Y - 3, Color = Brushes.Orange });
                }

                // 处理本提交的其他依赖
                for (int j = 1; j < commit.Parents.Count; j++) {
                    var parent = commit.Parents[j];
                    if (currentMap.ContainsKey(parent)) {
                        var l = currentMap[parent];
                        var link = new ShortLink();

                        link.Start = position;
                        link.End = new Point(l.HorizontalOffset, offsetY + HALF_HEIGHT);
                        link.Control = new Point(link.End.X, link.Start.Y);
                        link.Brush = l.Brush;
                        maker.Links.Add(link);
                    } else {
                        offsetX += UNIT_WIDTH;
                        unsolved.Add(new LineHelper(commit.Parents[j], isMerged, colorIdx, position));
                        colorIdx++;
                    }         
                }

                // 处理已终止的线
                foreach (var l in ended) {
                    l.AddPoint(position.X, position.Y, true);
                    maker.Lines.Add(l);
                    unsolved.Remove(l);
                }

                // 加入本次提交
                commit.IsMerged = isMerged;
                commit.GraphOffset = System.Math.Max(offsetX + HALF_WIDTH, oldCount * UNIT_WIDTH);

                // 清理临时数据
                ended.Clear();
                currentMap.Clear();
            }

            // 处理尚未终结的线
            for (int i = 0; i < unsolved.Count; i++) {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * UNIT_HEIGHT;

                if (path.Points.Count == 1 && path.Points[0].Y == endY) continue; 

                path.AddPoint((i + 0.5) * UNIT_WIDTH, endY, true);
                maker.Lines.Add(path);
            }
            unsolved.Clear();

            return maker;
        }
    }
}
