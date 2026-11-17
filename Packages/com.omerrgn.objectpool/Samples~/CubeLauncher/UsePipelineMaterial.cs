using UnityEngine;
using UnityEngine.Rendering;

namespace Omerrgn.ObjectPooling.Samples
{
    // Sample: the scene uses the Built-in Render Pipeline's default material, which URP and HDRP
    // cannot draw. This swaps in the default material of the pipeline the project uses instead
    [RequireComponent(typeof(Renderer))]
    public class UsePipelineMaterial : MonoBehaviour
    {
        private void Awake()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultMaterial != null)
            {
                GetComponent<Renderer>().sharedMaterial = pipeline.defaultMaterial;
            }
        }
    }
}
