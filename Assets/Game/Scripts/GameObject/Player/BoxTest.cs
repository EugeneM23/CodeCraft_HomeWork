using UnityEngine;

namespace Gameplay
{
    public class BoxTest : MonoBehaviour
    {
        private InputReader _reader;

        [Inject]
        public void Construct(InputReader reader)
        {
            _reader = reader;

            _reader.Log();
        }
    }
}