# Reflective MAC Ultra lens

The original lens Shader Graphs remain available as `SG_LensFakeDepth_URP.shadergraph` and `SG_LensFakeDepth_HDRP.shadergraph`. The MAC Ultra gobo prefabs use separate reflective variants, `SG_LensReflective_URP.shadergraph` and `SG_LensReflective_HDRP.shadergraph`, through `MovingLight_LensReflective_URP.mat` and `MovingLight_LensReflective_HDRP.mat`. The additive gobo surface remains the second material on the lens renderer.

The reflective variants use the pipeline-specific Lit targets. The lens material starts with metallic 0.6 and smoothness 0.94; these values control the environment reflection and can be tuned per fixture. The existing DMX color and dimmer remain connected to lens emission, and the separate gobo material remains DMX-controlled. The HDRP reflective Shader Graph opts in to receiving screen-space reflections.

For a visible reflection, provide an environment to reflect. A reflection probe that captures the intended surroundings should include the lens in its influence volume. In URP, enable Box Projection in the URP asset and on the probe when nearby objects should appear with positional parallax. For HDRP screen-space reflections, the HDRP asset, camera frame settings, and scene volume must also enable SSR; the material setting alone is insufficient. SSR can only reflect objects visible to the camera. Reflection probes remain the fallback for off-screen surroundings.

The demo scenes do not automatically include a reflection probe. A material or Shader Graph import/compile pass does not confirm the visual result; inspect the lens at multiple angles with gobo enabled and disabled in both pipelines.
