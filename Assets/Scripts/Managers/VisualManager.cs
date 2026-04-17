using System.Collections.Generic;
using UnityEngine;
using PrimeTween;
using System;

public class VisualManager : MonoBehaviour
{
    public static VisualManager Instance;

    [SerializeField] private GameObject[] pieces;

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
        return Instantiate(pieces[coreID], spawnPos, Quaternion.identity);
    }

    public void SwapPieces(GameObject pieceA, GameObject pieceB, Action onCompleteCallback = null)
    {
        Vector3 posA = pieceA.transform.position;
        Vector3 posB = pieceB.transform.position;

        TweenSettings settings = new TweenSettings(duration: 0.2f, ease: Ease.InOutCubic);

        Sequence.Create()
            .Group(Tween.Position(pieceA.transform, new TweenSettings<Vector3>(posB, settings)))
            .Group(Tween.Position(pieceB.transform, new TweenSettings<Vector3>(posA, settings)))
            .OnComplete(() => onCompleteCallback?.Invoke());
    }

    public void DestroyPieces(List<PieceData> pieces)
    {
        foreach (PieceData piece in pieces)
        {
            Tween.StopAll(piece.visualPiece);
            Destroy(piece.visualPiece);
        }
    }

    public void MovePiece(GameObject piece, int x, int y, Action onCompleteCallback = null)
    {
        TweenSettings settings = new TweenSettings(duration: .4f, ease: Ease.InCirc);

        // ADD LANDING ANIMATIONS
        Sequence.Create()
            .Group(Tween.Position(piece.transform, new TweenSettings<Vector3>(new Vector3(x, y), settings)))
            .OnComplete(() => onCompleteCallback?.Invoke());
    }

}
