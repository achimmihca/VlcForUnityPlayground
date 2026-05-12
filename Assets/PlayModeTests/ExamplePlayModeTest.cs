using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

public class ExamplePlayModeTest
{
    [UnityTest]
    public IEnumerator ShouldSucceed()
    {
        // VLC for Unity removes these log lines
        // after the test finished (see VLCNativePluginProcessor.PluginErrorCleaner.ClearPluginErrors)
        Debug.Log("Test started");
        Debug.Log("Test done");
        yield return null;
    }
}
