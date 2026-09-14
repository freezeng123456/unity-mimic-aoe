# Non-modal pointer test review · 036

The frozen source and its preserved `work/verification/036/before/NonModalPointerTests.cs` copy are byte-identical; both SHA-256 values are `02990eabf1e7a2f067d3c97e0613dcb9fbdbfe3a3c2cdd95779152b34a495cd5`. The reviewed file contains four PlayMode tests and uses real `NativeInputFixture` InputSystem state events, UGUI button centers, world points checked by `AssertWorldPoint`, and distinct pointer down/up sequences. There is no `onClick.Invoke`, sleep, or implementation mirror. The source includes timing assertions for the 220 ms seam and the post-action state assertions were retained in the frozen version.

Both old-runtime red runs are stable: `NonModal-red.xml` and `NonModal-red-repeat.xml` each report 1/4 passed and 3/4 failed. The same three semantic cases fail in both runs:

- after training, a fresh ground click does not select the Worker (`expected 131`, observed `130`);
- after cancelling training, a fresh resource click does not set the rally (`expected resource 131`, observed `0`);
- after the ATTACK MOVE button, the fresh ground click leaves `AttackMode` true instead of completing the action.

The fourth case, moving a UI press to the ground before release, passes in the old runtime, matching its separate click-through/capture contract. The three failures are therefore tied to the intended non-modal transition bug rather than a broad inability to process pointer input.

`NonModal-green.xml` reports all four cases passed (4/4), with the same test names and no skips or inconclusive results. This supports the narrow claim that the fixed runtime releases the ordinary non-modal button barrier before the following distinct world action. It does not establish modal behavior, OS focus/F9 recovery, native GUI behavior, or broader gameplay acceptance.

No source files were changed during this review and Unity was not launched.
