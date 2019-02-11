using System;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
#if UNITY_UWP
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
#endif
using CustomVision;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.WSA;
using UnityEngine.XR.WSA.Input;
using UnityEngine.XR.WSA.WebCam;

public class CustomVisionAnalyser : MonoBehaviour {

    /// <summary>
    /// Unique instance of this class
    /// </summary>
    public static CustomVisionAnalyser Instance;

    private ObjectDetection _objectDetection = null;
    
    /// <summary>
    /// Initializes this class
    /// </summary>
    private void Awake()
    {
        // Allows this instance to behave like a singleton
        Instance = this;
    }

    public async Task AnalyseONNX(string imagePath)
    {
#if UNITY_UWP
        try
        {
            if (!IsReady) return;
            Debug.Log("Analyzing...");

            SoftwareBitmap softwareBitmap = null;

            using (var stream = new InMemoryRandomAccessStream())
            using (var memStream = new InMemoryRandomAccessStream())
            {
                imageBytes = GetImageAsByteArray(imagePath);

                await stream.WriteAsync(imageBytes.AsBuffer());
                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);

                //コメント部分はなくてもいいらしい（入力が416x416画像のため縮尺を変更するロジック
                //BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(memStream, decoder);
                //encoder.BitmapTransform.ScaledWidth = 416;
                //encoder.BitmapTransform.ScaledHeight = 416;

                //await encoder.FlushAsync();
                //memStream.Seek(0);
                //var decorder = await BitmapDecoder.CreateAsync(memStream);

                softwareBitmap =
                    await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
            }

            _predictionModels =
                await _objectDetection.PredictImageAsync(VideoFrame.CreateWithSoftwareBitmap(softwareBitmap));
        }
        finally
        {
            // Stop the analysis process
            ImageCapture.Instance.ResetImageCapture();
        }
#endif
    }
    
    /// <summary>
    /// Returns the contents of the specified image file as a byte array.
    /// </summary>
    static byte[] GetImageAsByteArray(string imageFilePath)
    {
        FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);

        BinaryReader binaryReader = new BinaryReader(fileStream);

        return binaryReader.ReadBytes((int)fileStream.Length);
    }

    /// <summary>
    /// Bite array of the image to submit for analysis
    /// </summary>
    [HideInInspector] public byte[] imageBytes;

    private IList<PredictionModel> _predictionModels;

    // Use this for initialization
    void Start ()
    {
#if UNITY_UWP
        Task.Run(async () =>
        {
            var modelFile = await
                StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///Assets/LearningModel.onnx"));
            _objectDetection = new ObjectDetection(new List<string>(new[] { "AngelPie", "ChocoPie" }), 20, .3f, .45f);
            await _objectDetection.Init(modelFile);

            IsReady = true;
        });
#endif
    }


    public bool IsReady = false;

    // Update is called once per frame
    void Update () {

        if (IsReady && _predictionModels != null)
        {
            // Create a texture. Texture size does not matter, since
            // LoadImage will replace with the incoming image size.
            Texture2D tex = new Texture2D(1, 1);
            tex.LoadImage(imageBytes);
            SceneOrganiser.Instance.quadRenderer.material.SetTexture("_MainTex", tex);

            SceneOrganiser.Instance.FinaliseLabel(_predictionModels);
            _predictionModels = null;
        }
    }
}
