using System;
using UnityEngine;
using GoogleMobileAds;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;

public class AdMobManager : MonoBehaviour
{
    public static AdMobManager Instance { get; private set; }

    [Header("Boot")]
    [SerializeField] private bool dontDestroy = true;
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool useTestIds = true;

    [Header("Android Ad Unit IDs")]
    [SerializeField] private string androidBannerId = "ca-app-pub-2393527128341658/1302132126";
    [SerializeField] private string androidInterstitialId = "ca-app-pub-2393527128341658/9693853960";
    [SerializeField] private string androidRewardedId = "ca-app-pub-2393527128341658/1623792252";
    [SerializeField] private string androidRewardedInterstitialId = "ca-app-pub-2393527128341658/3584477275";

    [Header("iOS Ad Unit IDs")]
    [SerializeField] private string iosBannerId = "";
    [SerializeField] private string iosInterstitialId = "";
    [SerializeField] private string iosRewardedId = "";
    [SerializeField] private string iosRewardedInterstitialId = "";

    private BannerView bannerView;
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;
    private RewardedInterstitialAd rewardedInterstitialAd;

    private bool mobileAdsInitialized;
    private bool firstFullscreenLoadStarted;

    private Action pendingRewardedCallback;
    private Action pendingRewardedInterstitialCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroy)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (initializeOnStart)
            InitializeAds();
    }

    public void InitializeAds()
    {
        // UMP 동의 정보는 매 앱 실행 시 갱신하는 흐름이 공식 가이드 기준
        ConsentRequestParameters request = new ConsentRequestParameters();

        ConsentInformation.Update(request, OnConsentInfoUpdated);
    }

    private void OnConsentInfoUpdated(FormError error)
    {
        if (error != null)
            Debug.LogWarning("[AdMob] Consent update error: " + error.Message);

        ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) =>
        {
            if (formError != null)
                Debug.LogWarning("[AdMob] Consent form error: " + formError.Message);

            TryInitializeMobileAds();
        });
    }

    private void TryInitializeMobileAds()
    {
        if (!ConsentInformation.CanRequestAds())
        {
            Debug.Log("[AdMob] Cannot request ads yet.");
            return;
        }

        if (mobileAdsInitialized)
        {
            EnsureFullscreenAdsLoaded();
            return;
        }

        MobileAds.Initialize((InitializationStatus status) =>
        {
            mobileAdsInitialized = true;
            Debug.Log("[AdMob] MobileAds initialized.");

            EnsureFullscreenAdsLoaded();
        });
    }

    private void EnsureFullscreenAdsLoaded()
    {
        if (firstFullscreenLoadStarted)
            return;

        firstFullscreenLoadStarted = true;

        LoadInterstitial();
        LoadRewarded();
        LoadRewardedInterstitial();
    }

    #region Ad Unit IDs

    private string GetBannerId()
    {
#if UNITY_ANDROID
        return useTestIds ? "ca-app-pub-3940256099942544/9214589741" : androidBannerId; // adaptive banner test
#elif UNITY_IPHONE
        return useTestIds ? "ca-app-pub-3940256099942544/2435281174" : iosBannerId;
#else
        return "unused";
#endif
    }

    private string GetInterstitialId()
    {
#if UNITY_ANDROID
        return useTestIds ? "ca-app-pub-3940256099942544/1033173712" : androidInterstitialId;
#elif UNITY_IPHONE
        return useTestIds ? "ca-app-pub-3940256099942544/4411468910" : iosInterstitialId;
#else
        return "unused";
#endif
    }

    private string GetRewardedId()
    {
#if UNITY_ANDROID
        return useTestIds ? "ca-app-pub-3940256099942544/5224354917" : androidRewardedId;
#elif UNITY_IPHONE
        return useTestIds ? "ca-app-pub-3940256099942544/1712485313" : iosRewardedId;
#else
        return "unused";
#endif
    }

    private string GetRewardedInterstitialId()
    {
#if UNITY_ANDROID
        return useTestIds ? "ca-app-pub-3940256099942544/5354046379" : androidRewardedInterstitialId;
#elif UNITY_IPHONE
        return useTestIds ? "ca-app-pub-3940256099942544/6978759866" : iosRewardedInterstitialId;
#else
        return "unused";
#endif
    }

    #endregion

    #region Banner

    public void ShowBannerBottom()
    {
        Debug.Log("[AdMob] ShowBannerBottom called");

        if (!mobileAdsInitialized)
        {
            Debug.LogWarning("[AdMob] Not initialized yet.");
            return;
        }

        HideBanner();

        int safeWidth = MobileAds.Utils.GetDeviceSafeWidth();
        AdSize adaptiveSize =
            AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(safeWidth);

        bannerView = new BannerView(GetBannerId(), adaptiveSize, AdPosition.Bottom);
        RegisterBannerEvents(bannerView);
        bannerView.LoadAd(new AdRequest());

        Debug.Log("[AdMob] Banner load requested.");
    }

    public void ShowBannerTop()
    {
        if (!mobileAdsInitialized)
        {
            Debug.LogWarning("[AdMob] Not initialized yet.");
            return;
        }

        HideBanner();

        int safeWidth = MobileAds.Utils.GetDeviceSafeWidth();
        AdSize adaptiveSize =
            AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(safeWidth);

        bannerView = new BannerView(GetBannerId(), adaptiveSize, AdPosition.Top);
        RegisterBannerEvents(bannerView);
        bannerView.LoadAd(new AdRequest());

        Debug.Log("[AdMob] Banner load requested.");
    }

    public void HideBanner()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
            Debug.Log("[AdMob] Banner destroyed.");
        }
    }

    private void RegisterBannerEvents(BannerView view)
    {
        view.OnBannerAdLoaded += () =>
        {
            Debug.Log("[AdMob] Banner loaded.");
        };

        view.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            Debug.LogWarning("[AdMob] Banner load failed: " + error);
        };

        view.OnAdPaid += (AdValue value) =>
        {
            Debug.Log($"[AdMob] Banner paid event: {value.Value} {value.CurrencyCode}");
        };
    }

    #endregion

    #region Interstitial

    public void LoadInterstitial()
    {
        if (!mobileAdsInitialized) return;
        if (interstitialAd != null) return;

        InterstitialAd.Load(GetInterstitialId(), new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning("[AdMob] Interstitial load failed: " + error);
                interstitialAd = null;
                return;
            }

            interstitialAd = ad;
            RegisterInterstitialEvents(ad);
            Debug.Log("[AdMob] Interstitial loaded.");
        });
    }

    public bool CanShowInterstitial()
    {
        return interstitialAd != null && interstitialAd.CanShowAd();
    }

    public void ShowInterstitial()
    {
        if (CanShowInterstitial())
        {
            interstitialAd.Show();
        }
        else
        {
            Debug.Log("[AdMob] Interstitial not ready. Reloading...");
            LoadInterstitial();
        }
    }

    private void RegisterInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdPaid += (AdValue value) =>
        {
            Debug.Log($"[AdMob] Interstitial paid event: {value.Value} {value.CurrencyCode}");
        };

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[AdMob] Interstitial impression.");
        };

        ad.OnAdClicked += () =>
        {
            Debug.Log("[AdMob] Interstitial clicked.");
        };

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[AdMob] Interstitial opened.");
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdMob] Interstitial closed.");
            DestroyInterstitial();
            LoadInterstitial();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogWarning("[AdMob] Interstitial full screen failed: " + error);
            DestroyInterstitial();
            LoadInterstitial();
        };
    }

    private void DestroyInterstitial()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }
    }

    #endregion

    #region Rewarded

    public void LoadRewarded()
    {
        if (!mobileAdsInitialized) return;
        if (rewardedAd != null) return;

        RewardedAd.Load(GetRewardedId(), new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning("[AdMob] Rewarded load failed: " + error);
                rewardedAd = null;
                return;
            }

            rewardedAd = ad;
            RegisterRewardedEvents(ad);
            Debug.Log("[AdMob] Rewarded loaded.");
        });
    }

    public bool CanShowRewarded()
    {
        return rewardedAd != null && rewardedAd.CanShowAd();
    }

    public void ShowRewarded(Action onReward)
    {
        if (CanShowRewarded())
        {
            pendingRewardedCallback = onReward;

            rewardedAd.Show((Reward reward) =>
            {
                Debug.Log($"[AdMob] Rewarded reward: {reward.Type} / {reward.Amount}");
                pendingRewardedCallback?.Invoke();
                pendingRewardedCallback = null;
            });
        }
        else
        {
            Debug.Log("[AdMob] Rewarded not ready. Reloading...");
            LoadRewarded();
        }
    }

    private void RegisterRewardedEvents(RewardedAd ad)
    {
        ad.OnAdPaid += (AdValue value) =>
        {
            Debug.Log($"[AdMob] Rewarded paid event: {value.Value} {value.CurrencyCode}");
        };

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[AdMob] Rewarded impression.");
        };

        ad.OnAdClicked += () =>
        {
            Debug.Log("[AdMob] Rewarded clicked.");
        };

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[AdMob] Rewarded opened.");
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdMob] Rewarded closed.");
            DestroyRewarded();
            LoadRewarded();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogWarning("[AdMob] Rewarded full screen failed: " + error);
            DestroyRewarded();
            LoadRewarded();
        };
    }

    private void DestroyRewarded()
    {
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }
    }

    #endregion

    #region Rewarded Interstitial

    public void LoadRewardedInterstitial()
    {
        if (!mobileAdsInitialized) return;
        if (rewardedInterstitialAd != null) return;

        RewardedInterstitialAd.Load(GetRewardedInterstitialId(), new AdRequest(),
            (RewardedInterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[AdMob] RewardedInterstitial load failed: " + error);
                    rewardedInterstitialAd = null;
                    return;
                }

                rewardedInterstitialAd = ad;
                RegisterRewardedInterstitialEvents(ad);
                Debug.Log("[AdMob] RewardedInterstitial loaded.");
            });
    }

    public bool CanShowRewardedInterstitial()
    {
        return rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd();
    }

    /// <summary>
    /// 반드시 사전 안내 팝업(보상 내용 + 건너뛰기 가능)을 띄운 뒤,
    /// 유저가 '시청하기'를 눌렀을 때 이 메서드를 호출하세요.
    /// </summary>
    public void ShowRewardedInterstitial(Action onReward)
    {
        if (CanShowRewardedInterstitial())
        {
            pendingRewardedInterstitialCallback = onReward;

            rewardedInterstitialAd.Show((Reward reward) =>
            {
                Debug.Log($"[AdMob] RewardedInterstitial reward: {reward.Type} / {reward.Amount}");
                pendingRewardedInterstitialCallback?.Invoke();
                pendingRewardedInterstitialCallback = null;
            });
        }
        else
        {
            Debug.Log("[AdMob] RewardedInterstitial not ready. Reloading...");
            LoadRewardedInterstitial();
        }
    }

    private void RegisterRewardedInterstitialEvents(RewardedInterstitialAd ad)
    {
        ad.OnAdPaid += (AdValue value) =>
        {
            Debug.Log($"[AdMob] RewardedInterstitial paid event: {value.Value} {value.CurrencyCode}");
        };

        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[AdMob] RewardedInterstitial impression.");
        };

        ad.OnAdClicked += () =>
        {
            Debug.Log("[AdMob] RewardedInterstitial clicked.");
        };

        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[AdMob] RewardedInterstitial opened.");
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdMob] RewardedInterstitial closed.");
            DestroyRewardedInterstitial();
            LoadRewardedInterstitial();
        };

        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogWarning("[AdMob] RewardedInterstitial full screen failed: " + error);
            DestroyRewardedInterstitial();
            LoadRewardedInterstitial();
        };
    }

    private void DestroyRewardedInterstitial()
    {
        if (rewardedInterstitialAd != null)
        {
            rewardedInterstitialAd.Destroy();
            rewardedInterstitialAd = null;
        }
    }

    #endregion

    #region Privacy

    public bool IsPrivacyOptionsRequired()
    {
        return ConsentInformation.PrivacyOptionsRequirementStatus ==
               PrivacyOptionsRequirementStatus.Required;
    }

    public void ShowPrivacyOptionsForm()
    {
        ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
        {
            if (error != null)
                Debug.LogWarning("[AdMob] Privacy options form error: " + error.Message);
        });
    }

    #endregion

    private void OnDestroy()
    {
        HideBanner();
        DestroyInterstitial();
        DestroyRewarded();
        DestroyRewardedInterstitial();
    }
}