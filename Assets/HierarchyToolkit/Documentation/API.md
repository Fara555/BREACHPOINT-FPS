# Editor API and automatic rules

Place integrations in an Editor folder/Editor-only assembly. Reference HierarchyToolkit.Editor from an asmdef if needed.

```csharp
using HierarchyToolkit;
using UnityEngine;

// All persistent changes participate in Undo.
Toolkit.SetColor(target, new Color(0.3f, 0.7f, 0.6f));
Toolkit.SetColor(target, 0); // zero-based project palette index; invalid indexes throw
Toolkit.SetIcon(target, "toolkit:flag");
Toolkit.SetIcon(target, "Camera Icon"); // Unity Editor icon name
Toolkit.SetIcon(target, "asset:" + textureGuid);
Toolkit.SetBookmark(target, true);
Toolkit.Reset(target); // color/icon/kind only
Toolkit.CopyAppearance(source, duplicate); // includes descendants, excludes bookmarks
```

Icons use native Unity Editor keys such as `Folder Icon`, `Camera Icon`, `Light Icon` and `AudioSource Icon`. Old `toolkit:folder`, `toolkit:star`, `toolkit:diamond`, `toolkit:flag` keys are compatibility aliases to native icons; the generated glyphs were removed. Missing custom texture GUIDs produce no custom row icon and remain visible in palette settings.

```csharp
using HierarchyToolkit;
using UnityEngine;

static class MyHierarchyRules
{
    [HierarchyRule(priority: 10)]
    static void CameraRule(RuleContext context)
    {
        if (!context.gameObject.TryGetComponent<Camera>(out _)) return;
        context.icon = "Camera Icon";
        context.color = new Color(0.35f, 0.57f, 0.9f);
    }
}
```

Methods must be `static void Method(RuleContext)`. Context exposes GameObject, nullable Color, icon key and nullable DecorationKind. Rules run outside the GUI row callback, in ascending priority then method name; later assignments win among rules. Manual data wins over rules. Toggle all rules in Settings → Workflow. To disable a specific rule, remove/comment its attribute or add your own condition in that rule; call Toolkit.RefreshRules after changing external state.

Rules must not mutate scene objects or perform asset/file scans; keep them quick and deterministic. Runtime changes that do not publish an Editor object-change notification require Toolkit.RefreshRules. Exceptions disable the offending rule until domain reload.
