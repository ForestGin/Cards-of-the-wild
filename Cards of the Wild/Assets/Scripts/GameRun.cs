﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRun : MonoBehaviour
{

    // Management of sprites
    private Object[] backgrounds;
    private Object[] props;
    private Object[] chars;

    // Game management
    private GameObject enemyCards;
    private int[] enemyChars;
    private Agent agent;

    private GameObject playerCards;

    private int NUM_ENEMY_CARDS = 3;
    private int NUM_CLASSES = 3;
    private int DECK_SIZE = 4;

    // Rewards
    private float RWD_ACTION_INVALID = -2.0f;
    private float RWD_HAND_LOST = -1.0f;
    private float RWD_TIE = -0.1f;
    private float RWD_HAND_WON = 1.0f;

    // Other UI elements
    private UnityEngine.UI.Text textDeck;
    private UnityEngine.UI.Text textEnemyScore;
    private UnityEngine.UI.Text textPlayerScore;
    private UnityEngine.UI.Text textTiesScore;
    private UnityEngine.UI.Text textInvalidScore;

    int EnemyScore = 0;
    int PlayerScore = 0;
    int TiesScore = 0;
    int InvalidScore = 0;

    // Start is called before the first frame update
    void Start()
    {


        ///////////////////////////////////////
        // Sprite management
        ///////////////////////////////////////

        // Load all prefabs
        backgrounds = Resources.LoadAll("Backgrounds/");
        props = Resources.LoadAll("Props/");
        chars = Resources.LoadAll("Chars/");


        ///////////////////////////////////////
        // UI management
        ///////////////////////////////////////
        textDeck = GameObject.Find("TextDeck").GetComponent<UnityEngine.UI.Text>();
        textEnemyScore = GameObject.Find("TextEnemyScore").GetComponent<UnityEngine.UI.Text>();
        textPlayerScore = GameObject.Find("TextPlayerScore").GetComponent<UnityEngine.UI.Text>();
        textTiesScore = GameObject.Find("TextTiesScore").GetComponent<UnityEngine.UI.Text>();
        textInvalidScore = GameObject.Find("TextInvalidScore").GetComponent<UnityEngine.UI.Text>();

        ///////////////////////////////////////
        // Game management
        ///////////////////////////////////////
        enemyCards = GameObject.Find("EnemyCards");
        enemyChars = new int[NUM_ENEMY_CARDS];

        playerCards = GameObject.Find("PlayerCards");
        agent = GameObject.Find("AgentManager").GetComponent<Agent>();

        agent.Initialize();

        ///////////////////////////////////////
        // Start the game
        ///////////////////////////////////////
        StartCoroutine("GenerateTurn");


        ///////////////////////////////////////
        // Image generation
        ///////////////////////////////////////
        //renderTexture = gameObject.GetComponent<Camera>().targetTexture;

        //imgWidth  = renderTexture.width;
        //imgHeight = renderTexture.height;


    }


    // Generate a card on a given transform
    // Return the label (0-2) of the card
    private int GenerateCard(Transform parent)
    {

        int idx = Random.Range(0, backgrounds.Length);
        Instantiate(backgrounds[idx], parent.position, Quaternion.identity, parent);


        idx = Random.Range(0, props.Length);
        Vector3 position = new Vector3(Random.Range(-3.0f, 3.0f), Random.Range(-3.0f, 3.0f), -1.0f);
        Instantiate(props[idx], parent.position + position, Quaternion.identity, parent);

        idx = Random.Range(0, chars.Length);
        position = new Vector3(Random.Range(-3.0f, 3.0f), Random.Range(-3.0f, 3.0f), -2.0f);
        Instantiate(chars[idx], parent.position + position, Quaternion.identity, parent);

        // Determine label of the character, return it
        int label = 0;
        if (chars[idx].name.StartsWith("frog")) label = 1;
        else if (chars[idx].name.StartsWith("opposum")) label = 2;

        return label;
    }

    private void GeneratePlayerCard(Transform parent, int charindex)
    {

        int idx = Random.Range(0, backgrounds.Length);
        Instantiate(backgrounds[idx], parent.position, Quaternion.identity, parent);


        idx = Random.Range(0, props.Length);
        Vector3 position = new Vector3(Random.Range(-3.0f, 3.0f), Random.Range(-3.0f, 3.0f), -1.0f);
        Instantiate(props[idx], parent.position + position, Quaternion.identity, parent);

        idx = Random.Range(0, chars.Length / NUM_CLASSES);
        position = new Vector3(Random.Range(-3.0f, 3.0f), Random.Range(-3.0f, 3.0f), -2.0f);
        Instantiate(chars[charindex * (chars.Length / NUM_CLASSES) + idx], parent.position + position, Quaternion.identity, parent);

    }

    // Generate another turn
    IEnumerator GenerateTurn()
    {
        for (int turn = 0; turn < 100000; turn++)
        {

            ///////////////////////////////////////
            // Generate enemy cards
            ///////////////////////////////////////

            // Destroy previous sprites (if any) and generate new cards
            int c = 0;
            foreach (Transform card in enemyCards.transform)
            {
                foreach (Transform sprite in card)
                {
                    Destroy(sprite.gameObject);
                }

                enemyChars[c++] = GenerateCard(card);
            }


            ///////////////////////////////////////
            // Generate player deck
            ///////////////////////////////////////
            int[] deck = GeneratePlayerDeck();
            textDeck.text = "Deck: ";
            foreach (int card in deck)
                textDeck.text += card.ToString() + "/";

            textEnemyScore.text = "Wins: " + EnemyScore.ToString();
            textPlayerScore.text = "Wins: " + PlayerScore.ToString();
            textTiesScore.text = "Ties: " + TiesScore.ToString();
            textInvalidScore.text = "Invalid: " + InvalidScore.ToString();

            ///////////////////////////////////////
            // Tell the player to play
            ///////////////////////////////////////

            // IMPORTANT: wait until the frame is rendered so the player sees
            //            the newly generated cards (otherwise it will see the previous ones)
            yield return new WaitForEndOfFrame();

            int[] action = agent.Play(deck, enemyChars);

            textDeck.text += " Action:";
            foreach (int a in action)
                textDeck.text += a.ToString() + "/";


            int player = 0;
            foreach (Transform card in playerCards.transform)
            {
                foreach (Transform sprite in card)
                {
                    Destroy(sprite.gameObject);
                }

                GeneratePlayerCard(card, action[player]);
                player++;

            }

            ///////////////////////////////////////
            // Compute reward
            ///////////////////////////////////////
            float reward = ComputeReward(deck, action);

            Debug.Log("Turn/reward: " + turn.ToString() + "->" + reward.ToString());

            agent.GetReward(reward);


            ///////////////////////////////////////
            // Manage turns/games
            ///////////////////////////////////////



            yield return new WaitForSeconds(0.1f);

        }

    }


    // Auxiliary methods
    private int[] GeneratePlayerDeck()
    {
        int[] deck = new int[DECK_SIZE];

        for (int i = 0; i < DECK_SIZE; i++)
        {
            deck[i] = Random.Range(0, NUM_CLASSES);  // high limit is exclusive so [0, NUM_CLASSES-1]
        }

        return deck;
    }

    // Compute the result of the turn and return the associated reward 
    // given the cards selected by the agent (action)
    // deck -> array with the number of cards of each class the player has
    // action -> array with the class of each card played
    private float ComputeReward(int[] deck, int[] action)
    {
        // First check if the action is valid given the player's deck
        foreach (int card in action)
        {
            deck[card]--;
            if (deck[card] < 0)
            {
                InvalidScore++;
                return RWD_ACTION_INVALID;
            }
        }


        // Second see who wins
        int score = 0;
        for (int i = 0; i < NUM_ENEMY_CARDS; i++)
        {
            if (action[i] != enemyChars[i])
            {
                if (action[i] > enemyChars[i] || action[i] == 0 && enemyChars[i] == 2)
                    score++;
                else
                    score--;
            }

        }

        if (score == 0)
        {
            TiesScore++;
            return RWD_TIE;
        }
        else if (score > 0)
        {
            PlayerScore++;
            return RWD_HAND_WON;
        }
        else
        {
            EnemyScore++;
            return RWD_HAND_LOST;
        }
    }
}