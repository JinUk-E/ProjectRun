using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace MoraeGames.UI
{
    public sealed class LobbyHeroView : MonoBehaviour
    {
        public GameObject copBody, robberBody;
        public RawImage image;
        private GameObject _stage, _cop, _robber;
        private Camera _camera;
        private RenderTexture _texture;
        private bool _isCop = true;
        public void Select(bool cop) { _isCop=cop; if(_cop!=null){_cop.SetActive(cop);_robber.SetActive(!cop);} }
        private void Start()
        {
            _stage=new GameObject("LobbyHeroStage");_stage.transform.position=new Vector3(1000,1000,1000);
            _cop=Instantiate(copBody,_stage.transform);_robber=Instantiate(robberBody,_stage.transform);
            foreach(var t in _stage.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var light=new GameObject("HeroLight").AddComponent<Light>();light.transform.SetParent(_stage.transform,false);light.type=LightType.Directional;light.intensity=1.8f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(35,-35,0);
            _camera=new GameObject("LobbyHeroCamera").AddComponent<Camera>();_camera.transform.SetParent(_stage.transform,false);
            _camera.transform.localPosition=new Vector3(3,2.5f,7);_camera.transform.LookAt(_stage.transform.position+Vector3.up*1.15f);
            _camera.orthographic=true;_camera.orthographicSize=1.65f;_camera.cullingMask=1<<30;_camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=Color.clear;
            _texture=new RenderTexture(640,800,24);_camera.targetTexture=_texture;image.texture=_texture;Select(_isCop);
        }
        private void LateUpdate() { if(_stage!=null){float angle=Mathf.Sin(Time.unscaledTime*.6f)*9;_cop.transform.localRotation=Quaternion.Euler(0,angle,0);_robber.transform.localRotation=Quaternion.Euler(0,angle,0);} }
        private void OnDestroy(){if(_stage!=null)Destroy(_stage);if(_texture!=null){_texture.Release();Destroy(_texture);}}
    }

}
