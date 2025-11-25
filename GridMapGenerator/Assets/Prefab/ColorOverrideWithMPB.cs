using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class ColorOverrideWithMPB : MonoBehaviour
{
    private static readonly string[] DefaultColorProperties =
    {
        "_BaseColor",
        "_Color",
        "_TintColor"
    };

    [Header("Color Override")]
    public Color color = Color.white;

    [Tooltip("true면 _BaseColor/_Color/_TintColor 중에서 자동으로 찾습니다.")]
    public bool autoDetectProperty = true;

    [Tooltip("autoDetectProperty가 false일 때 사용할 프로퍼티 이름")]
    public string colorPropertyName = "_BaseColor";

    [SerializeField, HideInInspector]
    private string errorMessage;

    private new Renderer renderer;
    private MaterialPropertyBlock mpb;

    public string ErrorMessage => errorMessage;

    private void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 인스펙터에서 값 바꿀 때마다 즉시 반영
        Apply();
    }
#endif

    public void Apply()
    {
        if (renderer == null)
            renderer = GetComponent<Renderer>();

        if (renderer == null)
        {
            SetError("Renderer 컴포넌트를 찾을 수 없습니다.");
            return;
        }

        var material = renderer.sharedMaterial;
        if (material == null)
        {
            SetError("Renderer에 할당된 Material이 없습니다.");
            return;
        }

        // 사용할 프로퍼티 이름 결정
        string propName = null;

        if (autoDetectProperty)
        {
            foreach (var p in DefaultColorProperties)
            {
                if (material.HasProperty(p))
                {
                    propName = p;
                    break;
                }
            }

            if (propName == null)
            {
                SetError(
                    $"Material '{material.name}'에서 색상 프로퍼티를 찾을 수 없습니다.\n" +
                    $"시도한 이름: {string.Join(", ", DefaultColorProperties)}"
                );
                return;
            }
        }
        else
        {
            propName = colorPropertyName;

            if (string.IsNullOrEmpty(propName))
            {
                SetError("colorPropertyName이 비어 있습니다.");
                return;
            }

            if (!material.HasProperty(propName))
            {
                SetError(
                    $"Material '{material.name}'에 '{propName}' 프로퍼티가 없습니다."
                );
                return;
            }
        }

        ClearError();

        if (mpb == null)
            mpb = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(mpb);
        mpb.SetColor(propName, color);
        renderer.SetPropertyBlock(mpb);
    }

    private void ClearError()
    {
        errorMessage = string.Empty;
    }

    private void SetError(string msg)
    {
        errorMessage = msg;

        // 에러 상태에서는 기존 MPB를 지워서 예상치 못한 색 적용을 방지
        if (mpb != null)
        {
            mpb.Clear();
            renderer.SetPropertyBlock(mpb);
        }
    }
}
