using System.IO;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

// 동의 전에 광고 SDK가 초기화되지 않도록 play-services-ads의 MobileAdsInitProvider를 병합 매니페스트에서 뺀다.
// 기획서 §21.11.
//
// 이 구성요소는 앱이 켜질 때 광고 SDK를 스스로 초기화한다. 제거 표시(tools:node="remove")는 그 구성요소를 들여오는
// 라이브러리보다 우선순위가 높은 매니페스트에 있어야 먹힌다. play-services-ads는 unityLibrary가 가져오는 의존성이라,
// 같은 단계의 androidlib 폴더(AppConsent.androidlib)에 두면 순위가 같아 소용이 없었다(기기 빌드에서 확인).
// 그래서 Unity가 내보낸 unityLibrary의 매니페스트를 빌드 때마다 고쳐 넣는다.
public sealed class RemoveAdsInitProviderPostProcess : IPostGenerateGradleAndroidProject
{
    private const string ProviderName = "com.google.android.gms.ads.MobileAdsInitProvider";
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
    private static readonly XNamespace Tools = "http://schemas.android.com/tools";

    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"unityLibrary 매니페스트를 찾지 못해 광고 자동 초기화 제거를 건너뜁니다. : {manifestPath}");
            return;
        }

        XDocument document = XDocument.Load(manifestPath);
        XElement manifest = document.Root;
        XElement application = manifest?.Element("application");
        if (application == null)
        {
            Debug.LogWarning("매니페스트에 application 항목이 없어 광고 자동 초기화 제거를 건너뜁니다.");
            return;
        }

        // 이미 넣었으면 다시 넣지 않는다. 같은 내보내기 폴더를 다시 쓰는 빌드에서도 한 번만 들어간다.
        foreach (XElement provider in application.Elements("provider"))
        {
            if ((string)provider.Attribute(Android + "name") == ProviderName) return;
        }

        // tools 이름공간이 선언돼 있지 않으면 속성을 쓸 수 없다.
        if (manifest.Attribute(XNamespace.Xmlns + "tools") == null)
        {
            manifest.SetAttributeValue(XNamespace.Xmlns + "tools", Tools.NamespaceName);
        }

        application.Add(new XElement(
            "provider",
            new XAttribute(Android + "name", ProviderName),
            new XAttribute(Tools + "node", "remove")));
        document.Save(manifestPath);
    }
}
