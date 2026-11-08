var s = UnityEngine.Object.FindAnyObjectByType<TNDA.Prototype.IsoStage>();
s.yaw = float.Parse("YAW"); s.tilt = float.Parse("TILT"); s.orthoSize = float.Parse("ORTHO");
UnityEngine.ScreenCapture.CaptureScreenshot("tmp/OUT.png");
return "set";
