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

    [Header("Collectibles")]
    [SerializeField] private GameObject blueCandy;
    [SerializeField] private GameObject purpleCandy;
    [SerializeField] private GameObject yellowCandy;

    [Header("Obstacle")]
    [SerializeField] private GameObject goldenKeyObstacle;

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
        ObjectPoolManager.ReturnObjectToPool(piece);
    }

    public void MovePiece(GameObject piece, int x, int y, float startDelay, Action onCompleteCallback = null)
    {
        // Speed and easing combined
        float distance = Vector2.Distance(piece.transform.position, new Vector2(x, y));
        float calculatedDuration = distance * 0.14f;
        float finalDuration = Mathf.Clamp(calculatedDuration, 0.3f, 0.5f);

        TweenSettings settings = new TweenSettings(duration: finalDuration, ease: Ease.InQuad, startDelay: startDelay);

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

}
