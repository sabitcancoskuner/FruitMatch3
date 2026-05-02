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

    [Header("Visual Effects")]
    [SerializeField] private GameObject popVFX;
    [SerializeField] private GameObject bombVFX;
    [SerializeField] private LineRenderer discoVFX;

    private readonly Dictionary<Transform, Vector3> hintedOriginalScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Sequence> hintedSequences = new Dictionary<Transform, Sequence>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        PrimeTweenConfig.SetTweensCapacity(1000);
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
        // Vibrate if its a phone.
        #if UNITY_ANDROID
            HapticManager.Instance.VibrateLight();
        #endif

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
            Transform pieceTransform = piece.transform;

            if (hintedSequences.TryGetValue(pieceTransform, out Sequence hintSequence))
            {
                if (hintSequence.isAlive)
                {
                    hintSequence.Stop();
                }

                hintedSequences.Remove(pieceTransform);
            }

            if (hintedOriginalScales.TryGetValue(pieceTransform, out Vector3 originalScale))
            {
                pieceTransform.localScale = originalScale;
                hintedOriginalScales.Remove(pieceTransform);
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
        TweenSettings settings = new TweenSettings(duration: 0.08f, ease: Ease.InQuad);

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
                    AudioManager.Instance.PlaySFX(AudioManager.Instance.createPowerupSound);
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

            Sequence hintSequence = Sequence.Create(cycles: 4)
                .Group(Tween.Scale(target, highlightedScale, 0.2f, Ease.OutQuad))
                .Chain(Tween.Scale(target, originalScale, 0.16f, Ease.InOutSine));

            hintedSequences[target] = hintSequence;
        }
    }

    public void ClearHint()
    {
        if (hintedOriginalScales.Count == 0 && hintedSequences.Count == 0)
            return;

        foreach (Sequence hintSequence in hintedSequences.Values)
        {
            if (hintSequence.isAlive)
            {
                hintSequence.Stop();
            }
        }

        foreach (KeyValuePair<Transform, Vector3> pair in hintedOriginalScales)
        {
            Transform target = pair.Key;
            if (target == null) continue;

            if (target.gameObject.activeInHierarchy)
            {
                target.localScale = pair.Value;
            }
        }

        hintedSequences.Clear();
        hintedOriginalScales.Clear();
    }

    public void PlayMatchPopEffect(List<PieceData> pieces)
    {
        foreach (PieceData piece in pieces)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.matchPopSound);
            GameObject vfx = ObjectPoolManager.SpawnObject(popVFX, piece.visualPiece.transform.position, Quaternion.identity, PoolType.VFX);
            var vfxMain = vfx.GetComponent<ParticleSystem>().main;
            vfxMain.startColor = GetPieceColor(piece.coreID);
        }
    }

    public void PlayRocketEffect(GameObject piece, Vector2 direction, Action OnLaunchReadyCallback)
    {
        Transform half1 = piece.transform.GetChild(0);
        Transform half2 = piece.transform.GetChild(1);

        TrailRenderer trail1 = half1.GetComponentInChildren<TrailRenderer>();
        Vector3 firstHalfScale = half1.localScale;
        Vector3 firstHalfSquashScale = new Vector3(firstHalfScale.x * 1.3f, firstHalfScale.y * 0.7f);
        Vector3 firstHalfStretchScale = new Vector3(firstHalfScale.x * 0.7f, firstHalfScale.y * 1.4f);

        TrailRenderer trail2 = half2.GetComponentInChildren<TrailRenderer>();
        Vector3 secondHalfScale = half2.localScale;
        Vector3 secondHalfSquashScale = new Vector3(secondHalfScale.x * 1.3f, secondHalfScale.y * 0.7f);
        Vector3 secondHalfStretchScale = new Vector3(secondHalfScale.x * 0.7f, secondHalfScale.y * 1.4f);

        if (trail1 != null) trail1.emitting = false;
        if (trail2 != null) trail2.emitting = false;

        Sequence rocketSeq = Sequence.Create();

        if (direction == Vector2.up)
        {
            rocketSeq.Group(Tween.PositionY(half1, piece.transform.position.y - 0.2f, 0.15f, Ease.OutQuad))
                     .Group(Tween.PositionY(half2, piece.transform.position.y + 0.2f, 0.15f, Ease.OutQuad));
        }
        else
        {
            rocketSeq.Group(Tween.PositionX(half1, piece.transform.position.x - 0.2f, 0.15f, Ease.OutQuad))
                     .Group(Tween.PositionX(half2, piece.transform.position.x + 0.2f, 0.15f, Ease.OutQuad));
        }

        rocketSeq.Group(Tween.Scale(half1, firstHalfSquashScale, 0.15f, Ease.OutQuad))
                 .Group(Tween.Scale(half2, secondHalfSquashScale, 0.15f, Ease.OutQuad));

        rocketSeq.ChainCallback(() =>
        {
           AudioManager.Instance.PlaySFX(AudioManager.Instance.rocketPowerupSound);
           HapticManager.Instance.VibrateMedium();

           if (trail1 != null) trail1.emitting = true;
           if (trail2 != null) trail2.emitting = true;

           OnLaunchReadyCallback?.Invoke(); 
        });

        rocketSeq.Group(Tween.Scale(half1, firstHalfStretchScale, 0.1f))
                 .Group(Tween.Scale(half2, secondHalfStretchScale, 0.1f));

        if (direction == Vector2.up)
        {
            rocketSeq.Group(Tween.PositionY(half1, piece.transform.position.y + 10f, 0.4f, Ease.InCubic))
                     .Group(Tween.PositionY(half2, piece.transform.position.y - 10f, 0.4f, Ease.InCubic));
        }
        else
        {
            rocketSeq.Group(Tween.PositionX(half1, piece.transform.position.x + 10f, 0.4f, Ease.InCubic))
                     .Group(Tween.PositionX(half2, piece.transform.position.x - 10f, 0.4f, Ease.InCubic));
        }

        rocketSeq.OnComplete(() =>
        {
            half1.localScale = firstHalfScale;
            half1.localPosition = Vector3.zero;

            half2.localScale = secondHalfScale;
            half2.localPosition = Vector3.zero;

            if (trail1 != null)
            { 
                trail1.emitting = false;
                trail1.Clear();
            }
            if (trail2 != null)
            {
                trail2.emitting = false;
                trail2.Clear();
            }

            DestroyPiece(piece);
        });
    }

    public void PlayBombEffect(GameObject piece, Action OnCompleteCallback)
    {
        Transform bomb = piece.transform;
        Vector3 normalScale = bomb.localScale;
        Vector3 squashScale = new Vector3(normalScale.x * 1.3f, normalScale.y * 0.7f);
        Vector3 swellScale = new Vector3(normalScale.x * 1.4f, normalScale.y * 1.4f);

        Sequence.Create()
        .Group(Tween.Scale(bomb, squashScale, 0.12f, Ease.OutQuad))
        .Chain(Tween.Scale(bomb, swellScale, 0.08f, Ease.InQuad))
        .OnComplete(() =>
        {
            bomb.localScale = normalScale;
            ObjectPoolManager.SpawnObject(bombVFX, piece.transform.position, Quaternion.identity, PoolType.VFX);
            HapticManager.Instance.VibrateMedium();
            AudioManager.Instance.PlaySFX(AudioManager.Instance.bombPowerupSound);

            DestroyPiece(piece);
            OnCompleteCallback?.Invoke();            
        });
    }

    public void PlayPropellerEffect(GameObject piece, Vector3 endPos, Action OnCompleteCallback)
    {
        AudioSource vfxSource = AudioManager.Instance.PlaySFX(AudioManager.Instance.propellerPowerupSound);
        HapticManager.Instance.VibrateLight();

        SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = 1;

        Transform visual = piece.transform;
        Vector3 startPos = visual.position;
        Vector3 liftPos = startPos + new Vector3(0, 0.2f, 0);

        Vector3 midPoint = (liftPos + endPos) / 2f;

        float randomOffsetX = UnityEngine.Random.Range(-1.5f, -1.5f);
        float randomOffsetY = UnityEngine.Random.Range(0.5f, 1.5f);
        Vector3 controlPoint = new Vector3(midPoint.x + randomOffsetX, midPoint.y + randomOffsetY);

        Sequence.Create()
        .Group(Tween.Position(visual, liftPos, 0.2f, Ease.OutQuad))
        .Chain(Tween.Custom(0f, 1f, duration: 0.4f, ease: Ease.InQuad, onValueChange: t =>
        {
            visual.position = GetBezierPoint(t, liftPos, controlPoint, endPos);
        }))
        .OnComplete(() =>
        {
            vfxSource.Stop();
            renderer.sortingOrder = 0;
            DestroyPiece(piece);
            OnCompleteCallback?.Invoke();
        });
    }

    public void PlayDiscoBallEffect(GameObject piece, HashSet<GridNode> targets, Action<GridNode> OnExplosionReadyCallback, Action OnCompleteCallback = null)
    {
        Vector3 centerPosition = piece.transform.position;

        List<LineRenderer> activeLasers = new List<LineRenderer>();

        float timeBetweenLasers = 0.08f;
        float laserFlightDuration = 0.2f; 
        int delayIndex = 0;

        foreach (GridNode target in targets)
        {
            LineRenderer laser = ObjectPoolManager.SpawnObject(discoVFX, centerPosition, Quaternion.identity, PoolType.VFX);
            
            laser.SetPosition(0, centerPosition);
            laser.SetPosition(1, centerPosition); 

            Vector3 targetPos = new Vector3(target.xPosition, target.yPosition);
            
            float currentDelay = delayIndex * timeBetweenLasers;

            Tween.Custom(0f, 1f, duration: laserFlightDuration, startDelay: currentDelay, onValueChange: t =>
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.discoballPowerupLaserSound);
                if (laser != null)
                {
                    laser.SetPosition(1, Vector3.Lerp(centerPosition, targetPos, t));
                }
            })
            .OnComplete(() =>
            {
                OnExplosionReadyCallback(target);   
                HapticManager.Instance.VibrateLight();
                if (laser != null)
                {
                    // Optional: You can make the laser width shrink to 0 to make it "fizzle" out
                    Tween.Custom(laser.startWidth, 0f, 0.15f, onValueChange: width => 
                    {
                        if (laser != null)
                        {
                            laser.startWidth = width;
                            laser.endWidth = width;
                        }
                    })
                    .OnComplete(() => Destroy(laser.gameObject)); // Destroy AFTER the linger
                }
            });

            delayIndex++;
        }
        
        float totalSequenceTime = (targets.Count * timeBetweenLasers) + laserFlightDuration;
        
        Sequence.Create()
        .ChainDelay(totalSequenceTime)
        .OnComplete(() =>
        {
            DestroyPiece(piece);
            OnCompleteCallback?.Invoke();
        });
    }

    private Vector3 GetBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        
        return (uu * p0) + (2 * u * t * p1) + (tt * p2);
    }

    private Color GetPieceColor(int coreID)
    {
        switch (coreID)
        {
            case 1:
                return Color.orange;

            case 2:
                return Color.lightBlue;
            
            case 3:
                return Color.green;
            
            case 4:
                return Color.purple;

            case 5:
                return Color.red;

            default:
                return Color.white;
        }
    }

}
