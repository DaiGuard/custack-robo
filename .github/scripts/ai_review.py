import os
import sys
import google.generativeai as genai
import subprocess

def main():
    diff_file = sys.argv[1]
    with open(diff_file, 'r') as f:
        diff_text = f.read()

    genai.configure(api_key=os.environ["GEMINI_API_KEY"])
    model = genai.GenerativeModel('gemini-1.5-pro')
    
    prompt = f"""
    以下のコードの差分をレビューし、改善点やバグの可能性、
    またはパフォーマンス向上のためのアドバイスを日本語で簡潔に箇条書きで教えてください。
    
    {diff_text}
    """
    
    response = model.generate_content(prompt)
    review_comment = f"### 🤖 Gemini AI Code Review\n\n{response.text}"

    # GitHub CLIを使ってコメントを投稿
    subprocess.run([
        "gh", "pr", "comment", os.environ["GITHUB_PR_NUMBER"], 
        "--body", review_comment
    ])

if __name__ == "__main__":
    main()