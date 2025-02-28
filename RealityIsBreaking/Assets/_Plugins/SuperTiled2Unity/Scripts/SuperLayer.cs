﻿using UnityEngine;

namespace SuperTiled2Unity {
    public class SuperLayer : MonoBehaviour {
        [ReadOnly] public string m_TiledName;

        [ReadOnly] public float m_OffsetX;

        [ReadOnly] public float m_OffsetY;

        [ReadOnly] public float m_ParallaxX;

        [ReadOnly] public float m_ParallaxY;

        [ReadOnly] public float m_Opacity;

        [ReadOnly] public Color m_TintColor;

        [ReadOnly] public bool m_Visible;

        public Color CalculateColor() {
            var color = Color.white;

            foreach (var layer in gameObject.GetComponentsInParent<SuperLayer>()) {
                color *= layer.m_TintColor;
                color.a *= layer.m_Opacity;
            }

            return color;
        }

        public float CalculateOpacity() {
            var opacity = 1.0f;

            foreach (var layer in gameObject.GetComponentsInParent<SuperLayer>()) opacity *= layer.m_Opacity;

            return opacity;
        }
    }
}