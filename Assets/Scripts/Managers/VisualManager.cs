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
        GameObject piece = pieces[coreID];
        return ObjectPoolManager.SpawnObject(piece, spawnPos, Quaternion.identity);
    }

    public GameObject SpawnPowerup(int x, int y, PieceType type)
    {
        Vector3 spawnPos = new Vector3(x, y);
        GameObject powerup = GetPowerupObject(type);

        return ObjectPoolManager.SpawnObject(powerup, spawnPos, Quaternion.identity);
    }

    private GameObject GetPowerupObject(PieceType type)
    {
        switch (type)
        {
            case PieceType.VerticalRocket:
                return verticalRocket;

            case PieceType.HorizontalRocket:
                return horizontalRocket;

            case PieceType.Bomb:
                return bomb;

            case PieceType.DiscoBall:
                return discoBall;

            // SHOULD NOT RUN
            default:
                return pieces[0];
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
        ObjectPoolManager.ReturnObjectToPool(piece);
    }

    public void MovePiece(GameObject piece, int x, int y, float delay, Action onCompleteCallback = null)
    {
        // Speed and easing combined
        float distance = Vector2.Distance(piece.transform.position, new Vector2(x, y));
        float calculatedDuration = distance * 0.14f;
        float finalDuration = Mathf.Clamp(calculatedDuration, 0.3f, 0.5f);

        TweenSettings settings = new TweenSettings(duration: finalDuration, ease: Ease.InQuad, startDelay: delay);

        // Squash and stretch values for landing.
        Vector3 originalScale = piece.transform.localScale;
        Vector3 landScaleFirstPart = new Vector3(originalScale.x * 1.1f, originalScale.y * .9f, originalScale.z);
        Vector3 landScaleSecondPart = new Vector3(originalScale.x * .9f, originalScale.y * 1.1f, originalScale.z);

        // ADD LANDING ANIMATIONS
        Sequence.Create()
            .Group(Tween.Position(piece.transform, new TweenSettings<Vector3>(new Vector3(x, y), settings)))
            // .Chain(Tween.Scale(piece.transform, landScaleFirstPart, duration: .15f))
            // .Chain(Tween.Scale(piece.transform, landScaleSecondPart, duration: .15f))
            // .Chain(Tween.Scale(piece.transform, originalScale, duration: .1f))
            .OnComplete(() => onCompleteCallback?.Invoke());
    }

}
