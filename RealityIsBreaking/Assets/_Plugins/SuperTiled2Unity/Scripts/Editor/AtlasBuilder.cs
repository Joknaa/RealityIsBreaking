﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using Packer = SuperTiled2Unity.Editor.ThirdParty.MaxRectsBinPack;


namespace SuperTiled2Unity.Editor {
    // Creates a group of texture atlases for tiles
    // This is our best defense against visual artifacts for maps built on tiles
    // Users can decide in the TSX import settings to forgo atlases or use their own
    public class AtlasBuilder {
        private readonly int m_AtlasHeight;
        private readonly List<Texture2D> m_AtlasTextures = new List<Texture2D>();

        private readonly List<AtlasTile> m_AtlasTiles = new List<AtlasTile>();
        private readonly int m_AtlasWidth;
        private Texture2D m_CurrentAtlas;
        private Packer m_CurrentPacker;

        private readonly TiledAssetImporter m_TiledAssetImporter;
        private readonly SuperTileset m_TilesetScript;
        private readonly bool m_UseSpriteAtlas;


        public AtlasBuilder(TiledAssetImporter importer, bool useSpriteAtlas, int atlasWidth, int atlasHeight, SuperTileset tilesetScript) {
            m_TiledAssetImporter = importer;
            m_UseSpriteAtlas = useSpriteAtlas;
            m_AtlasWidth = atlasWidth;
            m_AtlasHeight = atlasHeight;
            m_TilesetScript = tilesetScript;
        }

        public void AddTile(int index, Texture2D texSource, Rect rcSource) {
            var atlasTile = new AtlasTile {Index = index, SourceTexture = texSource, SourceRectangle = rcSource};
            m_AtlasTiles.Add(atlasTile);
        }

        public void Build() {
            if (!m_AtlasTiles.Any()) return;

            if (m_UseSpriteAtlas) MakeAtlasTiles();

            // We have everything we need to create our sprites and tiles, including their texture dependencies. Commit.
            Commit();

            // Clear everything out
            m_AtlasTiles.Clear();
            m_AtlasTextures.Clear();
            m_CurrentAtlas = null;
        }

        private void MakeAtlasTiles() {
            // Order by area then Id
            var orderedTiles = m_AtlasTiles.OrderByDescending(t => t.SourceRectangle.width * t.SourceRectangle.height).ThenBy(t => t.Index).ToList();

            // Make sure the first tile even fits
            if (orderedTiles[0].SourceRectangle.width + 2 > m_AtlasWidth || orderedTiles[0].SourceRectangle.height + 2 > m_AtlasHeight) {
                m_TiledAssetImporter.ReportError("Atlas is not big enough to fit first tile. Try a larger atlas setting.");
                m_TiledAssetImporter.ReportError("Atlas size is ({0}, {1}) but we need at least ({2}, {3}).", m_AtlasWidth, m_AtlasHeight,
                    orderedTiles[0].SourceRectangle.width + 2, orderedTiles[0].SourceRectangle.height + 2);

                // By default we'll be using regular non-atlased tiles
                return;
            }

            // Create the first atlas and start feeding tiles into it
            PushAtlasTexture();

            foreach (var tile in orderedTiles) {
                var w = (int) tile.SourceRectangle.width;
                var h = (int) tile.SourceRectangle.height;
                var x = 0;
                var y = 0;

                // Figure out where we're going to pack the tile
                var atlasTexture = PackAtlasWithSize(w + 4, h + 4, out x, out y);

                if (atlasTexture != null) {
                    var rect = new Rect(x + 2, y + 2, w, h);

                    tile.AtlasRectangle = rect;
                    tile.AtlasTexture = m_CurrentAtlas;

                    // Blit the tile
                    tile.AtlasTexture.BlitRectFrom(rect.x, rect.y, tile.SourceTexture, tile.SourceRectangle);

                    // Adding a pad of pixels around our tile is the whole reason for atlases: It removes seams from our map.
                    // Add extra pixels to the left
                    var left = RectUtil.LeftEdge(rect);
                    tile.AtlasTexture.CopyOwnPixels(rect.x - 1, rect.y, left);
                    tile.AtlasTexture.CopyOwnPixels(rect.x - 2, rect.y, left);

                    // Our rectangle grows to the left
                    rect.x -= 2;
                    rect.width += 2;

                    // Add extra pixels to the top
                    var top = RectUtil.TopEdge(rect);
                    tile.AtlasTexture.CopyOwnPixels(rect.x, rect.yMax, top);
                    tile.AtlasTexture.CopyOwnPixels(rect.x, rect.yMax + 1, top);

                    // Our rectangle grows in height
                    rect.height += 2;

                    // Add extra pixels to the right
                    var right = RectUtil.RightEdge(rect);
                    tile.AtlasTexture.CopyOwnPixels(rect.xMax, rect.y, right);
                    tile.AtlasTexture.CopyOwnPixels(rect.xMax + 1, rect.y, right);

                    // Our rectangle grows in width
                    rect.width += 2;

                    // Add extra pixels to the bottom
                    var bottom = RectUtil.BottomEdge(rect);
                    tile.AtlasTexture.CopyOwnPixels(rect.x, rect.y - 1, bottom);
                    tile.AtlasTexture.CopyOwnPixels(rect.x, rect.y - 2, bottom);
                }
            }
        }

        private Texture2D PackAtlasWithSize(int width, int height, out int x, out int y) {
            Assert.IsNotNull(m_CurrentAtlas);
            Assert.IsNotNull(m_CurrentPacker);

            // Can we fit in the current texture atlas?
            if (!m_CurrentPacker.TryAreaFit(width, height, out x, out y)) {
                // We didn't fit in the current packer. Start a new one and try again
                PushAtlasTexture();

                // This should always succeed
                if (!m_CurrentPacker.TryAreaFit(width, height, out x, out y)) {
                    // This should never happen. If the tile is too big to fit then we failed to check it before we got here.
                    Debug.LogErrorFormat("Error: Failed to pack size ({0}, {1}) into secondary atlas. This should not have happend.", width, height);
                    return null;
                }
            }

            return m_CurrentAtlas;
        }

        private void PushAtlasTexture() {
            var textureName = string.Format("Atlas_{0}_{1}", m_TilesetScript.name, m_AtlasTextures.Count + 1);

            // Create the texture with a starter color that stands out
            m_CurrentAtlas = new Texture2D(m_AtlasWidth, m_AtlasHeight, TextureFormat.ARGB32, false);
            m_CurrentAtlas.wrapMode = TextureWrapMode.Clamp;
            m_CurrentAtlas.filterMode = FilterMode.Point;
            m_CurrentAtlas.name = textureName;
            m_CurrentAtlas.SetPixels32(Enumerable.Repeat(NamedColors.DeepPink, m_AtlasWidth * m_AtlasHeight).ToArray());

            // Add the texture to our import context and our list of textures
            var icon = SuperIcons.GetTsxIcon();
            m_TiledAssetImporter.SuperImportContext.AddObjectToAsset(textureName, m_CurrentAtlas, icon);
            m_AtlasTextures.Add(m_CurrentAtlas);

            // Start a new packer to go along with our atlas
            m_CurrentPacker = new Packer(m_AtlasWidth, m_AtlasHeight, false);
        }

        private void Commit() {
            // Done manipulating our atlas textures. Update all changes. No mipmaps and no more reading.
            m_AtlasTextures.ForEach(t => t.Apply(false, true));

            // Order tiles by Id
            var ordered = m_AtlasTiles.OrderBy(t => t.Index);

            foreach (var t in ordered) {
                var spriteName = string.Format("Sprite_{0}_{1}", m_TilesetScript.name, t.Index + 1);
                var tileName = string.Format("Tile_{0}_{1}", m_TilesetScript.name, t.Index + 1);

                // Create the sprite with the anchor at (0, 0)
                var sprite = Sprite.Create(t.PreferredTexture2D, t.PreferredRectangle, Vector2.zero, m_TiledAssetImporter.SuperImportContext.Settings.PixelsPerUnit);

                sprite.name = spriteName;
                sprite.hideFlags = HideFlags.HideInHierarchy;
                m_TiledAssetImporter.SuperImportContext.AddObjectToAsset(spriteName, sprite);

                // Create the tile that uses the sprite
                var tile = ScriptableObject.CreateInstance<SuperTile>();
                tile.m_TileId = t.Index;
                tile.name = tileName;
                tile.hideFlags = HideFlags.HideInHierarchy;
                tile.m_Sprite = sprite;
                tile.m_Width = t.SourceRectangle.width;
                tile.m_Height = t.SourceRectangle.height;
                tile.m_TileOffsetX = m_TilesetScript.m_TileOffset.x;
                tile.m_TileOffsetY = m_TilesetScript.m_TileOffset.y;
                tile.m_ObjectAlignment = m_TilesetScript.m_ObjectAlignment;

                m_TilesetScript.m_Tiles.Add(tile);
                m_TiledAssetImporter.SuperImportContext.AddObjectToAsset(tileName, tile);
            }
        }

        private class AtlasTile {
            public Rect AtlasRectangle;

            public Texture2D AtlasTexture;
            public int Index;
            public Rect SourceRectangle;
            public Texture2D SourceTexture;

            public Texture2D PreferredTexture2D {
                get {
                    if (AtlasTexture != null) return AtlasTexture;

                    return SourceTexture;
                }
            }

            public Rect PreferredRectangle => AtlasTexture != null ? AtlasRectangle : SourceRectangle;
        }
    }
}