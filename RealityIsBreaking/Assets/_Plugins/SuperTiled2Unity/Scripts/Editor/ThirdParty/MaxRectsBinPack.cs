﻿/*
 	Based on the Public Domain MaxRectsBinPack.cpp source by Jukka Jylänki
 	https://github.com/juj/RectangleBinPack/
 
 	Ported to C# by Sven Magnus
 	This version is also public domain - do whatever you want with it.
*/

// Seanba change: Put into namespace so we don't collide with other usage of this popular utility

using System.Collections.Generic;
using UnityEngine;

namespace SuperTiled2Unity.Editor.ThirdParty {
    public class MaxRectsBinPack {
        public enum FreeRectChoiceHeuristic {
            RectBestShortSideFit,

            ///< -BSSF: Positions the rectangle against the short side of a free rectangle into which it fits the best.
            RectBestLongSideFit,

            ///< -BLSF: Positions the rectangle against the long side of a free rectangle into which it fits the best.
            RectBestAreaFit,

            ///< -BAF: Positions the rectangle into the smallest free rect into which it fits.
            RectBottomLeftRule,

            ///< -BL: Does the Tetris placement.
            RectContactPointRule ///< -CP: Choosest the placement where the rectangle touches other rects as much as possible.
        }

        public bool allowRotations;
        public int binHeight;

        public int binWidth;
        public List<Rect> freeRectangles = new List<Rect>();

        public List<Rect> usedRectangles = new List<Rect>();

        public MaxRectsBinPack(int width, int height, bool rotations = true) {
            Init(width, height, rotations);
        }

        public void Init(int width, int height, bool rotations = true) {
            binWidth = width;
            binHeight = height;
            allowRotations = rotations;

            var n = new Rect();
            n.x = 0;
            n.y = 0;
            n.width = width;
            n.height = height;

            usedRectangles.Clear();

            freeRectangles.Clear();
            freeRectangles.Add(n);
        }

        public Rect Insert(int width, int height, FreeRectChoiceHeuristic method) {
            var newNode = new Rect();
            var score1 = 0; // Unused in this function. We don't need to know the score after finding the position.
            var score2 = 0;
            switch (method) {
                case FreeRectChoiceHeuristic.RectBestShortSideFit:
                    newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectBottomLeftRule:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectContactPointRule:
                    newNode = FindPositionForNewNodeContactPoint(width, height, ref score1);
                    break;
                case FreeRectChoiceHeuristic.RectBestLongSideFit:
                    newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1);
                    break;
                case FreeRectChoiceHeuristic.RectBestAreaFit:
                    newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2);
                    break;
            }

            if (newNode.height == 0)
                return newNode;

            var numRectanglesToProcess = freeRectangles.Count;
            for (var i = 0; i < numRectanglesToProcess; ++i)
                if (SplitFreeNode(freeRectangles[i], ref newNode)) {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectanglesToProcess;
                }

            PruneFreeList();

            usedRectangles.Add(newNode);
            return newNode;
        }

        public void Insert(List<Rect> rects, List<Rect> dst, FreeRectChoiceHeuristic method) {
            dst.Clear();

            while (rects.Count > 0) {
                var bestScore1 = int.MaxValue;
                var bestScore2 = int.MaxValue;
                var bestRectIndex = -1;
                var bestNode = new Rect();

                for (var i = 0; i < rects.Count; ++i) {
                    var score1 = 0;
                    var score2 = 0;
                    var newNode = ScoreRect((int) rects[i].width, (int) rects[i].height, method, ref score1, ref score2);

                    if (score1 < bestScore1 || score1 == bestScore1 && score2 < bestScore2) {
                        bestScore1 = score1;
                        bestScore2 = score2;
                        bestNode = newNode;
                        bestRectIndex = i;
                    }
                }

                if (bestRectIndex == -1)
                    return;

                PlaceRect(bestNode);
                rects.RemoveAt(bestRectIndex);
            }
        }

        private void PlaceRect(Rect node) {
            var numRectanglesToProcess = freeRectangles.Count;
            for (var i = 0; i < numRectanglesToProcess; ++i)
                if (SplitFreeNode(freeRectangles[i], ref node)) {
                    freeRectangles.RemoveAt(i);
                    --i;
                    --numRectanglesToProcess;
                }

            PruneFreeList();

            usedRectangles.Add(node);
        }

        private Rect ScoreRect(int width, int height, FreeRectChoiceHeuristic method, ref int score1, ref int score2) {
            var newNode = new Rect();
            score1 = int.MaxValue;
            score2 = int.MaxValue;
            switch (method) {
                case FreeRectChoiceHeuristic.RectBestShortSideFit:
                    newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectBottomLeftRule:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectContactPointRule:
                    newNode = FindPositionForNewNodeContactPoint(width, height, ref score1);
                    score1 = -score1; // Reverse since we are minimizing, but for contact point score bigger is better.
                    break;
                case FreeRectChoiceHeuristic.RectBestLongSideFit:
                    newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1);
                    break;
                case FreeRectChoiceHeuristic.RectBestAreaFit:
                    newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2);
                    break;
            }

            // Cannot fit the current rectangle.
            if (newNode.height == 0) {
                score1 = int.MaxValue;
                score2 = int.MaxValue;
            }

            return newNode;
        }

        /// Computes the ratio of used surface area.
        public float Occupancy() {
            ulong usedSurfaceArea = 0;
            for (var i = 0; i < usedRectangles.Count; ++i)
                usedSurfaceArea += (uint) usedRectangles[i].width * (uint) usedRectangles[i].height;

            return (float) usedSurfaceArea / (binWidth * binHeight);
        }

        private Rect FindPositionForNewNodeBottomLeft(int width, int height, ref int bestY, ref int bestX) {
            var bestNode = new Rect();
            //memset(bestNode, 0, sizeof(Rect));

            bestY = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i) {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    var topSideY = (int) freeRectangles[i].y + height;
                    if (topSideY < bestY || topSideY == bestY && freeRectangles[i].x < bestX) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestY = topSideY;
                        bestX = (int) freeRectangles[i].x;
                    }
                }

                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    var topSideY = (int) freeRectangles[i].y + width;
                    if (topSideY < bestY || topSideY == bestY && freeRectangles[i].x < bestX) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestY = topSideY;
                        bestX = (int) freeRectangles[i].x;
                    }
                }
            }

            return bestNode;
        }

        private Rect FindPositionForNewNodeBestShortSideFit(int width, int height, ref int bestShortSideFit, ref int bestLongSideFit) {
            var bestNode = new Rect();
            //memset(&bestNode, 0, sizeof(Rect));

            bestShortSideFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i) {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    var leftoverHoriz = Mathf.Abs((int) freeRectangles[i].width - width);
                    var leftoverVert = Mathf.Abs((int) freeRectangles[i].height - height);
                    var shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);

                    if (shortSideFit < bestShortSideFit || shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    var flippedLeftoverHoriz = Mathf.Abs((int) freeRectangles[i].width - height);
                    var flippedLeftoverVert = Mathf.Abs((int) freeRectangles[i].height - width);
                    var flippedShortSideFit = Mathf.Min(flippedLeftoverHoriz, flippedLeftoverVert);
                    var flippedLongSideFit = Mathf.Max(flippedLeftoverHoriz, flippedLeftoverVert);

                    if (flippedShortSideFit < bestShortSideFit || flippedShortSideFit == bestShortSideFit && flippedLongSideFit < bestLongSideFit) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestShortSideFit = flippedShortSideFit;
                        bestLongSideFit = flippedLongSideFit;
                    }
                }
            }

            return bestNode;
        }

        private Rect FindPositionForNewNodeBestLongSideFit(int width, int height, ref int bestShortSideFit, ref int bestLongSideFit) {
            var bestNode = new Rect();
            //memset(&bestNode, 0, sizeof(Rect));

            bestLongSideFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i) {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    var leftoverHoriz = Mathf.Abs((int) freeRectangles[i].width - width);
                    var leftoverVert = Mathf.Abs((int) freeRectangles[i].height - height);
                    var shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit || longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    var leftoverHoriz = Mathf.Abs((int) freeRectangles[i].width - height);
                    var leftoverVert = Mathf.Abs((int) freeRectangles[i].height - width);
                    var shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit || longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }

            return bestNode;
        }

        private Rect FindPositionForNewNodeBestAreaFit(int width, int height, ref int bestAreaFit, ref int bestShortSideFit) {
            var bestNode = new Rect();
            //memset(&bestNode, 0, sizeof(Rect));

            bestAreaFit = int.MaxValue;

            for (var i = 0; i < freeRectangles.Count; ++i) {
                var areaFit = (int) freeRectangles[i].width * (int) freeRectangles[i].height - width * height;

                // Try to place the rectangle in upright (non-flipped) orientation.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    var leftoverHoriz = Mathf.Abs((int) freeRectangles[i].width - width);
                    var leftoverVert = Mathf.Abs((int) freeRectangles[i].height - height);
                    var shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || areaFit == bestAreaFit && shortSideFit < bestShortSideFit) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }

                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    var leftoverHoriz = Mathf.Abs((int) freeRectangles[i].width - height);
                    var leftoverVert = Mathf.Abs((int) freeRectangles[i].height - width);
                    var shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || areaFit == bestAreaFit && shortSideFit < bestShortSideFit) {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }
            }

            return bestNode;
        }

        /// Returns 0 if the two intervals i1 and i2 are disjoint, or the length of their overlap otherwise.
        private int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end) {
            if (i1end < i2start || i2end < i1start)
                return 0;
            return Mathf.Min(i1end, i2end) - Mathf.Max(i1start, i2start);
        }

        private int ContactPointScoreNode(int x, int y, int width, int height) {
            var score = 0;

            if (x == 0 || x + width == binWidth)
                score += height;
            if (y == 0 || y + height == binHeight)
                score += width;

            for (var i = 0; i < usedRectangles.Count; ++i) {
                if (usedRectangles[i].x == x + width || usedRectangles[i].x + usedRectangles[i].width == x)
                    score += CommonIntervalLength((int) usedRectangles[i].y, (int) usedRectangles[i].y + (int) usedRectangles[i].height, y, y + height);
                if (usedRectangles[i].y == y + height || usedRectangles[i].y + usedRectangles[i].height == y)
                    score += CommonIntervalLength((int) usedRectangles[i].x, (int) usedRectangles[i].x + (int) usedRectangles[i].width, x, x + width);
            }

            return score;
        }

        private Rect FindPositionForNewNodeContactPoint(int width, int height, ref int bestContactScore) {
            var bestNode = new Rect();
            //memset(&bestNode, 0, sizeof(Rect));

            bestContactScore = -1;

            for (var i = 0; i < freeRectangles.Count; ++i) {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (freeRectangles[i].width >= width && freeRectangles[i].height >= height) {
                    var score = ContactPointScoreNode((int) freeRectangles[i].x, (int) freeRectangles[i].y, width, height);
                    if (score > bestContactScore) {
                        bestNode.x = (int) freeRectangles[i].x;
                        bestNode.y = (int) freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestContactScore = score;
                    }
                }

                if (allowRotations && freeRectangles[i].width >= height && freeRectangles[i].height >= width) {
                    var score = ContactPointScoreNode((int) freeRectangles[i].x, (int) freeRectangles[i].y, height, width);
                    if (score > bestContactScore) {
                        bestNode.x = (int) freeRectangles[i].x;
                        bestNode.y = (int) freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestContactScore = score;
                    }
                }
            }

            return bestNode;
        }

        private bool SplitFreeNode(Rect freeNode, ref Rect usedNode) {
            // Test with SAT if the rectangles even intersect.
            if (usedNode.x >= freeNode.x + freeNode.width || usedNode.x + usedNode.width <= freeNode.x ||
                usedNode.y >= freeNode.y + freeNode.height || usedNode.y + usedNode.height <= freeNode.y)
                return false;

            if (usedNode.x < freeNode.x + freeNode.width && usedNode.x + usedNode.width > freeNode.x) {
                // New node at the top side of the used node.
                if (usedNode.y > freeNode.y && usedNode.y < freeNode.y + freeNode.height) {
                    var newNode = freeNode;
                    newNode.height = usedNode.y - newNode.y;
                    freeRectangles.Add(newNode);
                }

                // New node at the bottom side of the used node.
                if (usedNode.y + usedNode.height < freeNode.y + freeNode.height) {
                    var newNode = freeNode;
                    newNode.y = usedNode.y + usedNode.height;
                    newNode.height = freeNode.y + freeNode.height - (usedNode.y + usedNode.height);
                    freeRectangles.Add(newNode);
                }
            }

            if (usedNode.y < freeNode.y + freeNode.height && usedNode.y + usedNode.height > freeNode.y) {
                // New node at the left side of the used node.
                if (usedNode.x > freeNode.x && usedNode.x < freeNode.x + freeNode.width) {
                    var newNode = freeNode;
                    newNode.width = usedNode.x - newNode.x;
                    freeRectangles.Add(newNode);
                }

                // New node at the right side of the used node.
                if (usedNode.x + usedNode.width < freeNode.x + freeNode.width) {
                    var newNode = freeNode;
                    newNode.x = usedNode.x + usedNode.width;
                    newNode.width = freeNode.x + freeNode.width - (usedNode.x + usedNode.width);
                    freeRectangles.Add(newNode);
                }
            }

            return true;
        }

        private void PruneFreeList() {
            for (var i = 0; i < freeRectangles.Count; ++i)
            for (var j = i + 1; j < freeRectangles.Count; ++j) {
                if (IsContainedIn(freeRectangles[i], freeRectangles[j])) {
                    freeRectangles.RemoveAt(i);
                    --i;
                    break;
                }

                if (IsContainedIn(freeRectangles[j], freeRectangles[i])) {
                    freeRectangles.RemoveAt(j);
                    --j;
                }
            }
        }

        private bool IsContainedIn(Rect a, Rect b) {
            return a.x >= b.x && a.y >= b.y
                              && a.x + a.width <= b.x + b.width
                              && a.y + a.height <= b.y + b.height;
        }

        // Seanba: My extensions
        public bool TryAreaFit(int width, int height, out int x, out int y) {
            var rect = Insert(width, height, FreeRectChoiceHeuristic.RectBestAreaFit);
            if (rect.width == 0 || rect.height == 0) {
                x = 0;
                y = 0;
                return false;
            }

            x = (int) rect.x;
            y = (int) rect.y;

            return true;
        }
    }
}