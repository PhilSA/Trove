

# Score compensation

When calculating scores, a "score compensation" is applied in case there is a difference in the amount of enabled considerations between different actions of a certain reasoner.

Consider this scenario:
* Action1 has the following consideration scores: `0.5f`, `0.8f`
* Action2 has the following consideration scores: `0.5f`, `0.8f`, `0.8f`, `0.8f`, `0.8f`
* If we compute the scores without compensation, we will get these final scores:
    * Action1: `0.5f` * `0.8f` = `0.4f`
    * Action2: `0.5f` * `0.8f` * `0.8f` * `0.8f` * `0.8f` = `0.2048f`
* As you can see, Action2 gets a significantly lower final score simply because it has more considerations (even though its consideration scores are similar to Action1's). This wouldn't make sense.
* The "score compensation" mechanism calculates the difference in considerations count between an action, and the action that thas the most considerations in this reasoner. Then, it will multiply the value of its highest consideration score to the action's final score for each difference in count with the action that has the most considerations. Here are the scores with compensation (scores in parentheses represent scores added by compensation):
    * Action1: `0.5f` * `0.8f` * (`0.8f`) * (`0.8f`) * (`0.8f`) = `0.2048f`
    * Action2: `0.5f` * `0.8f` *  `0.8f`  *  `0.8f`  *  `0.8f`  = `0.2048f`
* This also preserves the property of consideration scores where a score of `1f` does not increase or decrease an action's score, no matter how many times it is applied:
    * Action1: `1.0f` * `0.2f` * (`1.0f`) * (`1.0f`) * (`1.0f`) = `0.2f`
    * Action2: `0.2f` * `1.0f` *  `1.0f`  *  `1.0f`  *  `1.0f`  = `0.2f`
* As you can see, with score compensation, actions are no longer penalized for having more considerations. Just be aware when designing your consideration curves that highest scores have priority. For example, be aware that if Action2's extra considerations score `0.5f` instead of `0.8f`, then Action1 will get a much higher score than Action2:
    * Action1: `0.5f` * `0.8f` * (`0.8f`) * (`0.8f`) * (`0.8f`) = `0.2048f`
    * Action2: `0.5f` * `0.8f` *  `0.5f`  *  `0.5f`  *  `0.5f`  = `0.0500f`
    * Action3: `0.5f` * `0.8f` *  `0.6f`  *  `1.0f` *   `0.8f`  = `0.1920f`
* It's important to be aware of the multiplicative nature of consideration scores, and know that they aren't processed like an "average". This is why Action3 scores lower than Action1, even though their average consideration scores would be the same. It can be more useful to interpret a score of `0.8f` as meaning "adding a 20% importance reduction" rather than meaning "a high score". Two `0.8f` scores (adding two "20% reductions") do not average out to one 20% reduction; they combine into a 36% reduction (reduce `1f` by 20% -> `0.8f`, then reduce `0.8f` by 20% -> `0.64f`). The reason why actions with fewer considerations are compensated using their highest-scoring value is because this is the value that would not "reduce the importance" of the action relatively to other actions any more than what was intentionally defined.    