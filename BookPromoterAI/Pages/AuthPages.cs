namespace BookPromoterAI;

static class AuthPages
{
    // Reusable eye-toggle script (plain non-interpolated string so JS
    // braces don't get misread as C# interpolation holes).
    const string EyeToggleScript = """
        <script>
        function togglePassword(inputId, btn) {
            var input = document.getElementById(inputId);
            if (input.type === 'password') {
                input.type = 'text';
                btn.textContent = 'Hide';
            } else {
                input.type = 'password';
                btn.textContent = 'Show';
            }
        }
        </script>
        """;

    public static string StartLogin(string notice) => $"""
        <section class="hero">
            <div>
                <p class="eyebrow">Welcome</p>
                <h1>Log in or create an account.</h1>
            </div>
        </section>
        {notice}
        <section class="split">
            <form method="post" action="/login" class="panel form">
                <h1>Log In</h1>
                <label>Email <input name="email" type="email" required></label>
                <label>Password
                    <div class="password-field">
                        <input id="login-password" name="password" type="password" required>
                        <button type="button" class="show-password-btn" onclick="togglePassword('login-password', this)">Show</button>
                    </div>
                </label>
                <button class="button" type="submit">Log In</button>
                <p class="muted small-text">
                    <a href="/forgot-password">Forgot your password?</a> &middot;
                    Need a 30-day access code? <a href="/trial">Request one here.</a>
                </p>
            </form>
            <form method="post" action="/signup" class="panel form">
                <h1>Create Account</h1>
                <p class="muted">Set up an account, then choose an access code or subscription plan.</p>
                <label>Email <input name="email" type="email" required></label>
                <label>Password
                    <div class="password-field">
                        <input id="signup-password" name="password" type="password" required minlength="6">
                        <button type="button" class="show-password-btn" onclick="togglePassword('signup-password', this)">Show</button>
                    </div>
                </label>
                <p class="muted small-text">At least 6 characters. You choose your own password &mdash; we don't generate one for you.</p>
                <label class="checkbox-label legal-accept-checkbox">
                    <input type="checkbox" name="acceptTerms" value="true" required>
                    <span>I have read and agree to the <a href="/terms" target="_blank" rel="noopener">Terms &amp; Conditions</a>.</span>
                </label>
                <button class="button" type="submit">Create Account</button>
            </form>
        </section>
        """ + EyeToggleScript;

    public static string TrialRequest(string notice) => $"""
        <section class="split">
            <form method="post" action="/trial/request" class="panel form">
                <h1>Get Your Access Code</h1>
                <p class="muted">Enter your email address and we'll send you a 30-day access code. The code is assigned to your email and can only be used once.</p>
                {notice}
                <label>Email Address <input name="email" type="email" placeholder="you@example.com" required></label>
                <button class="button" type="submit">Send My Access Code</button>
                <p class="muted small-text">Already have a code? <a href="/trial/activate">Enter it here.</a></p>
            </form>
            <section class="panel">
                <h1>How It Works</h1>
                <p>Enter your email and we'll generate a unique access code just for you.</p>
                <p>Check your inbox for the code, then come back here and enter it to unlock 30 days of full access.</p>
                <p>Each code is tied to one email address and cannot be shared or reused.</p>
            </section>
        </section>
        """;

    public static string TrialActivate(string email, string notice) => $"""
        <section class="split">
            <form method="post" action="/trial/activate" class="panel form">
                <h1>Activate Your Access Code</h1>
                <p class="muted">Enter the access code we sent to your email address below.</p>
                {notice}
                <label>Email Address <input name="email" type="email" value="{H.Encode(email)}" placeholder="you@example.com" required></label>
                <label>Access Code <input name="promoCode" placeholder="ACCESS-XXXXXX" required></label>
                <button class="button" type="submit">Activate Access</button>
                <p class="muted small-text">Haven't requested a code yet? <a href="/trial">Get one here.</a></p>
            </form>
            <section class="panel">
                <h1>Need a Code?</h1>
                <p>Access codes are sent to your email when you request them on the previous step.</p>
                <p>If your code isn't arriving, check your spam folder or <a href="/trial">request a new one.</a></p>
            </section>
        </section>
        """;

    // Step 1: User enters their email to request a reset link
    public static string ForgotPassword(string notice) => $"""
        <section class="split">
            <form method="post" action="/forgot-password" class="panel form">
                <h1>Forgot Password</h1>
                <p class="muted">Enter the email address you signed up with. If an account exists we'll send you a reset link valid for 1 hour.</p>
                {notice}
                <label>Email Address <input name="email" type="email" placeholder="you@example.com" required></label>
                <button class="button" type="submit">Send Reset Link</button>
                <p class="muted small-text"><a href="/start">Back to Log In</a></p>
            </form>
            <section class="panel">
                <h1>Didn't get the email?</h1>
                <p>Check your spam or junk folder first.</p>
                <p>If it still hasn't arrived after a few minutes, try submitting again.</p>
                <p>Make sure you're using the same email address you signed up with.</p>
            </section>
        </section>
        """;

    // Step 2: User clicks the link and sets a new password
    public static string ResetPassword(string token, string notice) => $"""
        <section class="split">
            <form method="post" action="/reset-password" class="panel form">
                <h1>Set New Password</h1>
                <p class="muted">Enter your new password below. It must be at least 6 characters.</p>
                {notice}
                <input type="hidden" name="token" value="{H.Encode(token)}">
                <label>New Password
                    <div class="password-field">
                        <input id="new-password" name="newPassword" type="password" required minlength="6">
                        <button type="button" class="show-password-btn" onclick="togglePassword('new-password', this)">Show</button>
                    </div>
                </label>
                <button class="button" type="submit">Set New Password</button>
            </form>
            <section class="panel">
                <h1>Reset Link</h1>
                <p>This link expires 1 hour after it was sent. If it has expired, <a href="/forgot-password">request a new one.</a></p>
            </section>
        </section>
        """ + EyeToggleScript;
}
