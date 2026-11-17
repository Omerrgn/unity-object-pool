using UnityEngine;
using UnityEngine.Rendering;

namespace Omerrgn.ObjectPooling.Samples
{
    // Sample: the scene uses the Built-in Render Pipeline's default material, which URP and HDRP
    // cannot draw. This swaps in the default material of the pipeline the project uses, then tints it
    [RequireComponent(typeof(Renderer))]
    public class SampleMaterial : MonoBehaviour
    {
        [SerializeField] private Color color = Color.white;

        private void Awake()
        {
            var meshRenderer = GetComponent<Renderer>();

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultMaterial != null)
            {
                meshRenderer.sharedMaterial = pipeline.defaultMaterial;
            }

            // Material.color sets whichever property the shader marks as its main color
            meshRenderer.material.color = color;
        }
    }
}
