using System.Collections.Generic;
using UnityEngine;
using PrimeTween;
using System;

public class VisualManager : MonoBehaviour
{
    public static VisualManager Instance;

    [SerializeField] private GameObject[] pieces;

    [Header("Super Powers")]
    [SerializeField] private GameObject verticalRocket;
    [SerializeField] private GameObject horizontalRocket;
    [SerializeField] private GameObject bomb;
    [SerializeField] private GameObject discoBall;
    [SerializeField] private GameObject propeller;

    [Header("Collectibles")]
    [SerializeField] private GameObject blueCandy;
    [SerializeField] private GameObject purpleCandy;
    [SerializeField] private GameObject yellowCandy;

    [Header("Obstacle")]
    [SerializeField] private GameObject goldenKeyObstacle;

    private readonly Dictionary<Transform, Vector3> hintedOriginalScales = new Dictionary<Transform, Vector3>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    public GameObject SpawnPiece(int x, int y, int coreID)
    {
        Vector3 spawnPos = new Vector3(x, y);
        GameObject obj = GetObjectToSpawn(coreID);

        if (obj == null)
        {
            Debug.LogError("Something wrong with id of the piece, can not get the game object");
            return null;
        }

        return ObjectPoolManager.SpawnObject(obj, spawnPos, Quaternion.identity);
    }

    private GameObject GetObjectToSpawn(int coreID)
    {
        switch (coreID)
        {
            // Normal pieces
            case 1:
                return pieces[0];

            case 2:
                return pieces[1];

            case 3:
                return pieces[2];

            case 4:
                return pieces[3];

            case 5:
                return pieces[4];

            // Powerups
            case 100:
                return verticalRocket;

            case 200:
                return horizontalRocket;

            case 300:
                return bomb;

            case 400:
                return discoBall;

            case 500:
                return propeller;

            // Collectibles
            case 1000:
                return blueCandy;
            
            case 2000:
                return purpleCandy;

            case 3000:
                return yellowCandy;

            // Obstacles
            case 10000:
                return goldenKeyObstacle;

            default:
                return null;    
        }
    }

    public void SwapPieces(GameObject pieceA, GameObject pieceB, Vector3 targetA, Vector3 targetB, Action onCompleteCallback = null)
    {
        TweenSettings settings = new TweenSettings(duration: 0.1f, ease: Ease.InOutCubic);

        Sequence.Create()
            .Group(Tween.Position(pieceA.transform, new TweenSettings<Vector3>(targetA, settings)))
            .Group(Tween.Position(pieceB.transform, new TweenSettings<Vector3>(targetB, settings)))
            .OnComplete(() => onCompleteCallback?.Invoke());
    }

    public void DestroyPieces(List<PieceData> pieces)
    {
        foreach (PieceData piece in pieces)
        {
            DestroyPiece(piece.visualPiece);
        }
    }

    public void DestroyPiece(GameObject piece)
    {
        if (piece != null)
        {
            if (hintedOriginalScales.TryGetValue(piece.transform, out Vector3 originalScale))
            {
                piece.transform.localScale = originalScale;
                hintedOriginalScales.Remove(piece.transform);
            }
        }

        ObjectPoolManager.ReturnObjectToPool(piece);
    }

    public void MovePiece(GameObject piece, int x, int y, float startDelay, Action onCompleteCallback = null)
    {
        // Sometimes game tries to move inactive objects. Because it can be destroyed by a powerup when falling.
        if (piece == null || !piece.activeInHierarchy)
        {
            onCompleteCallback?.Invoke();
            return;
        }

        // Speed and easing combined
        float distance = Vector2.Distance(piece.transform.position, new Vector3(x, y));
        float calculatedDuration = distance * 0.14f;
        float finalDuration = Mathf.Clamp(calculatedDuration, 0.3f, 0.5f);

        Sequence.Create()
            .Group(Tween.Position(piece.transform, endValue: new Vector3(x, y), duration: finalDuration, ease: Ease.InQuad, startDelay: startDelay))
            .OnComplete(() => 
            {
                if (piece != null && piece.activeInHierarchy)
                {
                    OnLandAnimation(piece);
                }

                onCompleteCallback?.Invoke();
            });
    }

    public void OnLandAnimation(GameObject piece)
    {
        if (piece == null || !piece.activeInHierarchy)
        {
            return;
        }

        // Squash and stretch values for landing.
        Vector3 originalScale = piece.transform.localScale;
        Vector3 landScale = new Vector3(originalScale.x * 1.2f, originalScale.y * .8f, originalScale.z);
        Vector3 stretchScale = new Vector3(originalScale.x * .9f, originalScale.y * 1.1f, originalScale.z);

        TweenSettings smashDownSettings = new TweenSettings(duration: 0.08f, ease: Ease.OutQuad);
        TweenSettings stretchSettings = new TweenSettings(duration: 0.1f, ease: Ease.InOutSine);
        TweenSettings normalSettings = new TweenSettings(duration: 0.08f, ease: Ease.OutBounce);

        Sequence.Create()
        .Group(Tween.Scale(piece.transform, new TweenSettings<Vector3>(landScale, smashDownSettings)))
        .Chain(Tween.Scale(piece.transform, new TweenSettings<Vector3>(stretchScale, stretchSettings)))
        .Chain(Tween.Scale(piece.transform, new TweenSettings<Vector3>(originalScale, normalSettings)));
    }

    public void ShakeAtPosition(GameObject piece, Vector2 direction)
    {
        Vector3 startPosition = piece.transform.position;

        float shakeStrength = .15f;
        float xMultiplier = direction.x * shakeStrength;
        float yMultiplier = direction.y * shakeStrength;

        float xPositive = piece.transform.position.x + xMultiplier;
        float xNegative = piece.transform.position.x + xMultiplier * -1;

        float yPositive = piece.transform.position.y + yMultiplier;
        float yNegative = piece.transform.position.y + yMultiplier * -1;


        Sequence.Create()
            .Chain(Tween.Position(piece.transform, endValue: new Vector3(xPositive, yPositive), duration: 0.03f))
            .Chain(Tween.Position(piece.transform, endValue: new Vector3(xNegative, yNegative), duration: 0.06f))
            .Chain(Tween.Position(piece.transform, endValue: startPosition, duration: .03f));
    }

    public void CombinePieces(List<PieceData> pieces, Vector3 center, Action OnCompleteCallback = null)
    {
        TweenSettings settings = new TweenSettings(duration: 0.1f, ease: Ease.InQuad);

        for(int i = 0; i < pieces.Count; i++)
        {
            GameObject piece = pieces[i].visualPiece;
            if (piece.transform.position == center) continue;

            /* Storing i value because after all the animations are done it needs to call callback on the last moved piece.
               if it is not stored and we check i value with count i will always be equal to the length of the list.  */
            int index = i;

            Sequence.Create()
            .Group(Tween.Position(piece.transform, new TweenSettings<Vector3>(new Vector3(center.x, center.y), settings)))
            .OnComplete(() =>
            {
                if (index == pieces.Count - 1)
                {
                    OnCompleteCallback?.Invoke();
                }
            });
        }
    }

    public void HintMatch(List<PieceData> pieces, PieceData pieceToSwap)
    {
        ClearHint();

        List<PieceData> piecesToAnimate = new List<PieceData>();

        if (pieceToSwap != null)
            piecesToAnimate.Add(pieceToSwap);

        if (pieces != null)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                PieceData piece = pieces[i];
                if (piece == null) continue;
                if (piecesToAnimate.Contains(piece)) continue;
                piecesToAnimate.Add(piece);
            }
        }

        if (piecesToAnimate.Count == 0)
            return;

        for (int i = 0; i < piecesToAnimate.Count; i++)
        {
            PieceData piece = piecesToAnimate[i];
            GameObject pieceVisual = piece.visualPiece;

            if (pieceVisual == null || !pieceVisual.activeInHierarchy)
                continue;

            Transform target = pieceVisual.transform;
            if (!hintedOriginalScales.ContainsKey(target))
                hintedOriginalScales.Add(target, target.localScale);

            bool isSwapPiece = piece == pieceToSwap;
            float scaleMultiplier = isSwapPiece ? 1.22f : 1.12f;
            Vector3 originalScale = hintedOriginalScales[target];
            Vector3 highlightedScale = originalScale * scaleMultiplier;

            Sequence.Create(cycles: 4)
                .Group(Tween.Scale(target, highlightedScale, 0.2f, Ease.OutQuad))
                .Chain(Tween.Scale(target, originalScale, 0.16f, Ease.InOutSine));
        }
    }

    public void ClearHint()
    {
        if (hintedOriginalScales.Count == 0)
            return;

        foreach (KeyValuePair<Transform, Vector3> pair in hintedOriginalScales)
        {
            Transform target = pair.Key;
            if (target == null) continue;

            Tween.StopAll(onTarget: target);

            if (target.gameObject.activeInHierarchy)
            {
                target.localScale = pair.Value;
            }
        }

        hintedOriginalScales.Clear();
    }

}
