import tkinter as tk
import os
import ctypes

def main():
    # 在Windows上禁用DPI缩放（解决高DPI屏幕问题）
    if os.name == 'nt':
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(2)
        except:
            pass
    
    # 创建主窗口
    root = tk.Tk()
    root.title("testWindow")
    
    # 设置窗口大小为 1920x1080
    root.geometry("1920x1080")
    
    # 可选：设置窗口在屏幕中央
    root.update_idletasks()
    width = root.winfo_width()
    height = root.winfo_height()
    x = (root.winfo_screenwidth() // 2) - (width // 2)
    y = (root.winfo_screenheight() // 2) - (height // 2)
    root.geometry(f'+{x}+{y}')
    
    # 创建一个标签显示窗口信息
    label = tk.Label(root, text="testWindow", font=("Arial", 24))
    label.pack(expand=True)
    
    # 运行主循环
    root.mainloop()

if __name__ == "__main__":
    main()
