using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class AfterImagesUI : MonoBehaviour
{
    [SerializeField]
    private int count = 4;
    [SerializeField]
    private AnimationCurve falloffCurve;
    [SerializeField]
    private Vector2 translation;
    [SerializeField]
    private Vector2 scale;

    [SerializeField]
    private float refreshRate = 20f;

    private Image[] images;
    private Image selfImage;

    private Coroutine updateRoutine;

    private void Awake()
    {
        selfImage = GetComponent<Image>();
        images = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var obj = new GameObject();
            obj.transform.parent = transform.parent;
            obj.transform.SetSiblingIndex(transform.GetSiblingIndex() - 1);
            var img = obj.AddComponent<Image>();
            
            images[i] = img;
            var c = img.color;
            c.a = 1f - falloffCurve.Evaluate((i + 1f) / (count + 1f));
            img.color = c;
        }
    }

    private void OnEnable()
    {
        updateRoutine = StartCoroutine(UpdateAfterImages());
    }

    IEnumerator UpdateAfterImages()
    {
        while (true)
        {
            var otherSprite = selfImage.sprite;
            var position = transform.position;

            var lastPosition = position;
            for (int i = 0; i < count; i++)
            {
                // swap sprites.
                var img = images[i];
                var tspr = img.sprite;
                img.sprite = otherSprite;
                // update positions
                var rt = img.transform as RectTransform;
                var tPos = rt.position;
                //var tAPos = rt.anchoredPosition;

                rt.position = lastPosition;
                rt.anchoredPosition += translation * (1f / count);
                rt.localScale = Vector3.one + (Vector3) (((float)i/count)*scale);
                lastPosition = tPos;
            }
            yield return new WaitForSeconds(1f/refreshRate);
        }
    }

    private void Update()
    {
        foreach (var image in images)
        {
            var rt = image.transform as RectTransform;
            rt.anchoredPosition += translation * (Time.deltaTime * refreshRate);
            rt.localScale += (Vector3)scale * (Time.deltaTime * refreshRate);
        }
    }

    private void OnDisable()
    {
        StopCoroutine(updateRoutine);
    }
}
