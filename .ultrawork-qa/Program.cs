using System;
using System.Linq;
using CharacterStudio.Core;

internal static class Program
{
    private static int Main()
    {
        try
        {
            var cfg = new PawnFaceConfig();
            cfg.EnsureDefaultOverlayRules();

            var scaredExpr = cfg.expressionOverlayRules.First(r => r.expression == ExpressionType.Scared);
            var scaredRoute = cfg.emotionOverlayRules.First(r => string.Equals(r.semanticKey, "scared", StringComparison.OrdinalIgnoreCase));

            scaredRoute.overlayIds.Clear();
            scaredRoute.overlayIds.Add("Sweat");
            scaredRoute.overlayIds.Add("Tear");
            scaredRoute.overlayId = scaredRoute.overlayIds.First();

            var ids = cfg.ResolveOverlayIds(scaredExpr.semanticKey, ExpressionType.Scared);

            Console.WriteLine($"semantic={scaredExpr.semanticKey}");
            Console.WriteLine($"count={ids.Count}");
            Console.WriteLine($"ids={string.Join(",", ids)}");

            return ids.Count == 2 && ids.Contains("Sweat") && ids.Contains("Tear") ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("qa_exception=" + ex.GetType().FullName);
            Console.WriteLine("qa_message=" + ex.Message);
            if (ex is System.Reflection.ReflectionTypeLoadException rtl && rtl.LoaderExceptions != null)
            {
                foreach (Exception loader in rtl.LoaderExceptions)
                {
                    if (loader == null) continue;
                    Console.WriteLine("loader_exception=" + loader.GetType().FullName);
                    Console.WriteLine("loader_message=" + loader.Message);
                }
            }
            return 2;
        }
    }
}
