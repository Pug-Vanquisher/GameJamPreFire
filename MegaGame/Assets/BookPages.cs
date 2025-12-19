using UnityEngine;
using System.Collections;
using Events;

public class BookPages : MonoBehaviour
{
    public bool isMoving;
    public bool isOpened;

    public GameObject closedBook;
    public GameObject openedBook;

    public float duration;

    public Vector3[] OpenedBookPosition;
    public Vector3[] ClosedBookPosition;

    public MeshRenderer mesh;

    public Texture[] pages;

    private int page = 0;

    void Start()
    {
        EventBus.Subscribe<BookInterracted>(Interacted);
    }


    public void Interacted(BookInterracted bookEvent)
    {
        if (!isMoving)
        {
            if (bookEvent.input == "book" && !isOpened)
            {
                StartCoroutine(openBook());
                isMoving = true;
            }
            else if (bookEvent.input == null && isOpened)
            {
                StartCoroutine(closeBook());
                isMoving = true;
            }
        }
        if (bookEvent.input == "page1" || bookEvent.input == "page2")
        {
            Debug.Log(bookEvent.input + " page=" + page + " pages.lenght="+ pages.Length);
            if (bookEvent.input == "page1" && page - 2 >= 0)
            {
                page -= 2;
            }
            if (bookEvent.input == "page2" && page + 2 < pages.Length)
            {
                page += 2;
            }
            mesh.materials[2].mainTexture = pages[page];
            mesh.materials[4].mainTexture = pages[page+1];
        }
    }

    public IEnumerator closeBook()
    {
        float _step = 0.01f;
        float _duration = duration;

        for(float i = 0; i < _duration; i += _step)
        {
            openedBook.transform.localPosition = Vector3.Lerp(OpenedBookPosition[1], OpenedBookPosition[0], i / duration);
            closedBook.transform.localPosition = Vector3.Lerp(ClosedBookPosition[1], ClosedBookPosition[0], i / duration);

            yield return new WaitForSeconds(_step);
        }

        isOpened = false;
        isMoving = false;
    }

    public IEnumerator openBook()
    {
        float _step = 0.01f;
        float _duration = duration;

        for (float i = 0; i < _duration; i += _step)
        {
            openedBook.transform.localPosition = Vector3.Lerp(OpenedBookPosition[0], OpenedBookPosition[1], i / duration);
            closedBook.transform.localPosition = Vector3.Lerp(ClosedBookPosition[0], ClosedBookPosition[1], i / duration);

            yield return new WaitForSeconds(_step);
        }

        isOpened = true;
        isMoving = false;
    }
}
